using System.Diagnostics;
using System.Text.RegularExpressions;
using A52.DeviceManager.Models;

namespace A52.DeviceManager.Services;

/// <summary>
/// Implementação do serviço de comunicação via ADB CLI reutilizando a arquitetura do PhonEx.
/// Suporta descoberta de dispositivos ADB conectados e seleção dinâmica.
/// </summary>
public class AdbDeviceService : IA52DeviceService
{
    private string _targetSerial;
    private bool _bridgeVerified;
    public event EventHandler<TspTelemetry>? TelemetryUpdated;

    public string CurrentTargetDevice => _targetSerial;

    public AdbDeviceService(string targetSerial = "RQ8T601E01X")
    {
        _targetSerial = targetSerial;
    }

    public void SetTargetDevice(string serial)
    {
        if (_targetSerial != serial)
        {
            _targetSerial = serial;
            _bridgeVerified = false;
        }
    }

    public async Task<List<string>> GetAvailableDevicesAsync(CancellationToken ct = default)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "adb",
                Arguments = "devices",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            string output = await stdoutTask;

            var devices = new List<string>();
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase))
                    continue;

                var match = Regex.Match(trimmed, @"^([^\s]+)\s+(device|offline|unauthorized)");
                if (match.Success)
                {
                    devices.Add(match.Groups[1].Value);
                }
            }
            return devices;
        }
        catch
        {
            return new List<string>();
        }
    }

    public async Task EnsureBridgeRunningAsync(CancellationToken ct = default)
    {
        try
        {
            var (_, psOut, _) = await RunAdbAsync("shell \"ps -A | grep a52-daemon\"", ct);
            if (!psOut.Contains("a52-daemon"))
            {
                string localScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "a52-daemon.sh");
                if (File.Exists(localScript))
                {
                    await RunAdbAsync($"push \"{localScript}\" /data/local/tmp/a52-daemon.sh", ct);
                }
                await RunAdbAsync("shell \"dos2unix /data/local/tmp/a52-daemon.sh 2>/dev/null || sed -i 's/\\r$//' /data/local/tmp/a52-daemon.sh; chmod 755 /data/local/tmp/a52-daemon.sh; nohup sh /data/local/tmp/a52-daemon.sh >/dev/null 2>&1 &\"", ct);
            }
            _bridgeVerified = true;
        }
        catch { }
    }

    private async Task<(int ExitCode, string StdOut, string StdErr)> RunAdbAsync(string args, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "adb",
            Arguments = string.IsNullOrEmpty(_targetSerial) ? args : $"-s {_targetSerial} {args}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

    public async Task<TspTelemetry> PollTelemetryAsync(CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_targetSerial))
            {
                var devs = await GetAvailableDevicesAsync(ct);
                if (devs.Count > 0)
                {
                    _targetSerial = devs[0];
                }
                else
                {
                    var noDevice = new TspTelemetry { DeviceConnected = false, DeviceSerial = "Nenhum dispositivo ADB" };
                    TelemetryUpdated?.Invoke(this, noDevice);
                    return noDevice;
                }
            }

            if (!_bridgeVerified)
            {
                _ = Task.Run(() => EnsureBridgeRunningAsync(ct), ct);
            }

            var (codeHw, hwRaw, _) = await RunAdbAsync("shell cat /sys/class/sec/tsp/hw_param", ct);
            if (codeHw != 0 || string.IsNullOrWhiteSpace(hwRaw))
            {
                var disconnected = new TspTelemetry { DeviceConnected = false, DeviceSerial = _targetSerial, Uptime = "Desconectado" };
                TelemetryUpdated?.Invoke(this, disconnected);
                return disconnected;
            }

            var (_, sensRaw, _) = await RunAdbAsync("shell cat /sys/class/sec/tsp/sensitivity_mode", ct);
            var (_, slotsRaw, _) = await RunAdbAsync("shell \"dumpsys input 2>/dev/null | grep -c 'inUse=true'\"", ct);
            var (_, scrubRaw, _) = await RunAdbAsync("shell cat /sys/class/sec/tsp/scrub_pos", ct);
            var (_, uptimeRaw, _) = await RunAdbAsync("shell uptime", ct);

            int tnoi = 0, tmul = 0, twet = 0;
            var matchHw = Regex.Match(hwRaw, @"""TMUL"":""?(\d+)""?.*""TWET"":""?(\d+)""?.*""TNOI"":""?(\d+)""?");
            if (matchHw.Success)
            {
                int.TryParse(matchHw.Groups[1].Value, out tmul);
                int.TryParse(matchHw.Groups[2].Value, out twet);
                int.TryParse(matchHw.Groups[3].Value, out tnoi);
            }

            int[] nodes = new int[9];
            var parts = sensRaw.Trim().Split(',', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < Math.Min(9, parts.Length); i++)
            {
                int.TryParse(parts[i].Trim(), out nodes[i]);
            }

            int.TryParse(slotsRaw.Trim(), out int activeSlots);

            var telemetry = new TspTelemetry
            {
                DeviceConnected = true,
                DeviceSerial = _targetSerial,
                Uptime = uptimeRaw.Trim(),
                Tnoi = tnoi,
                Tmul = tmul,
                Twet = twet,
                ActiveTouchSlots = activeSlots,
                LastScrubPos = scrubRaw.Trim(),
                FodNodes = nodes,
                RawSensitivityMode = sensRaw.Trim()
            };

            TelemetryUpdated?.Invoke(this, telemetry);
            return telemetry;
        }
        catch
        {
            _bridgeVerified = false;
            var disconnected = new TspTelemetry { DeviceConnected = false, DeviceSerial = _targetSerial, Uptime = "Erro de Leitura" };
            TelemetryUpdated?.Invoke(this, disconnected);
            return disconnected;
        }
    }

    public async Task<bool> UnlockTouchAsync(CancellationToken ct = default)
    {
        var (code1, _, _) = await RunAdbAsync("shell service call SemInputDeviceManagerService 29 i32 1 i32 1 s16 \"module_off_master\"", ct);
        await Task.Delay(1000, ct);
        var (code2, _, _) = await RunAdbAsync("shell service call SemInputDeviceManagerService 29 i32 1 i32 1 s16 \"module_on_master\"", ct);
        await Task.Delay(500, ct);
        await RunAdbAsync("shell service call SemInputDeviceManagerService 29 i32 1 i32 1 s16 \"set_fod_rect,421,2018,659,2256\"", ct);
        await RunAdbAsync("shell service call SemInputDeviceManagerService 29 i32 1 i32 1 s16 \"fod_enable,1,1,0\"", ct);
        return code1 == 0 && code2 == 0;
    }

    public async Task<bool> CycleDisplayPowerAsync(CancellationToken ct = default)
    {
        await RunAdbAsync("shell input keyevent 223", ct);
        await Task.Delay(2000, ct);
        await RunAdbAsync("shell input keyevent 224", ct);
        return true;
    }

    public async Task<bool> ApplyHardeningSettingsAsync(CancellationToken ct = default)
    {
        await RunAdbAsync("shell settings put system any_screen_enabled 0", ct);
        await RunAdbAsync("shell settings put system any_screen_running 0", ct);
        await RunAdbAsync("shell settings put system touch_sensitivity_mode 0", ct);
        await RunAdbAsync("shell settings put system double_tab_to_wake_up 0", ct);
        await RunAdbAsync("shell settings put secure double_tap_to_wake 0", ct);
        await RunAdbAsync("shell settings put system aod_tap_to_show_mode 0", ct);
        await RunAdbAsync("shell settings put system screen_off_pocket 0", ct);
        await RunAdbAsync("shell settings put system show_touches 0", ct);
        return true;
    }

    public async Task<bool> RebootDeviceAsync(CancellationToken ct = default)
    {
        var (code, _, _) = await RunAdbAsync("reboot", ct);
        return code == 0;
    }
}
