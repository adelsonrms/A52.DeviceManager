using System.Diagnostics;
using System.Text.RegularExpressions;
using A52.DeviceManager.Models;

namespace A52.DeviceManager.Services;

/// <summary>
/// Implementação do serviço de comunicação via ADB CLI reutilizando a arquitetura testada no PhonEx.
/// </summary>
public class AdbDeviceService : IA52DeviceService
{
    private readonly string _targetSerial;
    public event EventHandler<TspTelemetry>? TelemetryUpdated;

    public AdbDeviceService(string targetSerial = "RQ8T601E01X")
    {
        _targetSerial = targetSerial;
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
            var (_, hwRaw, _) = await RunAdbAsync("shell cat /sys/class/sec/tsp/hw_param", ct);
            var (_, sensRaw, _) = await RunAdbAsync("shell cat /sys/class/sec/tsp/sensitivity_mode", ct);
            var (_, slotsRaw, _) = await RunAdbAsync("shell \"dumpsys input | grep 'inUse=true' | wc -l\"", ct);
            var (_, scrubRaw, _) = await RunAdbAsync("shell cat /sys/class/sec/tsp/scrub_pos", ct);
            var (_, uptimeRaw, _) = await RunAdbAsync("shell uptime", ct);

            int tnoi = 0, tmul = 0, twet = 0;
            var matchHw = Regex.Match(hwRaw, @"""TMUL"":""(\d+)"",""TWET"":""(\d+)"",""TNOI"":""(\d+)""");
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
                FodNodes = nodes
            };

            TelemetryUpdated?.Invoke(this, telemetry);
            return telemetry;
        }
        catch
        {
            var disconnected = new TspTelemetry { DeviceConnected = false, DeviceSerial = _targetSerial };
            TelemetryUpdated?.Invoke(this, disconnected);
            return disconnected;
        }
    }

    public async Task<bool> UnlockTouchAsync(CancellationToken ct = default)
    {
        // 1. Desliga o módulo via Binder
        var (code1, _, _) = await RunAdbAsync("shell service call SemInputDeviceManagerService 29 i32 1 i32 1 s16 \"module_off_master\"", ct);
        await Task.Delay(1000, ct);
        // 2. Religa o módulo
        var (code2, _, _) = await RunAdbAsync("shell service call SemInputDeviceManagerService 29 i32 1 i32 1 s16 \"module_on_master\"", ct);
        await Task.Delay(500, ct);
        // 3. Reativa a zona FOD
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
