using A52.DeviceManager.Models;

namespace A52.DeviceManager.Services;

/// <summary>
/// Provedor nativo Android que lê diretamente os nós /sys expostos pelo kernel no dispositivo.
/// </summary>
public class LocalKernelService : IA52DeviceService
{
    public event EventHandler<TspTelemetry>? TelemetryUpdated;

    public Task<TspTelemetry> PollTelemetryAsync(CancellationToken ct = default)
    {
        try
        {
            string hwRaw = ReadSysfs("/sys/class/sec/tsp/hw_param");
            string sensRaw = ReadSysfs("/sys/class/sec/tsp/sensitivity_mode");
            string scrubRaw = ReadSysfs("/sys/class/sec/tsp/scrub_pos");

            int tnoi = 0, tmul = 0, twet = 0;
            var matchHw = System.Text.RegularExpressions.Regex.Match(hwRaw, @"""TMUL"":""(\d+)"",""TWET"":""(\d+)"",""TNOI"":""(\d+)""");
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

            var telemetry = new TspTelemetry
            {
                DeviceConnected = true,
                DeviceSerial = "Local (On-Device)",
                Uptime = "Local System",
                Tnoi = tnoi,
                Tmul = tmul,
                Twet = twet,
                ActiveTouchSlots = 0,
                LastScrubPos = scrubRaw.Trim(),
                FodNodes = nodes
            };

            TelemetryUpdated?.Invoke(this, telemetry);
            return Task.FromResult(telemetry);
        }
        catch
        {
            var telemetry = new TspTelemetry
            {
                DeviceConnected = true,
                DeviceSerial = "Local (On-Device)",
                Uptime = "Modo Local"
            };
            TelemetryUpdated?.Invoke(this, telemetry);
            return Task.FromResult(telemetry);
        }
    }

    private static string ReadSysfs(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }
        catch { }
        return string.Empty;
    }

    public Task<bool> UnlockTouchAsync(CancellationToken ct = default) => Task.FromResult(false);
    public Task<bool> CycleDisplayPowerAsync(CancellationToken ct = default) => Task.FromResult(false);
    public Task<bool> ApplyHardeningSettingsAsync(CancellationToken ct = default) => Task.FromResult(false);
    public Task<bool> RebootDeviceAsync(CancellationToken ct = default) => Task.FromResult(false);
}
