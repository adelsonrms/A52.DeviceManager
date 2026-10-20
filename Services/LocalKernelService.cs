using System.Text.Json;
using System.Text.RegularExpressions;
using A52.DeviceManager.Models;

namespace A52.DeviceManager.Services;

/// <summary>
/// Provedor nativo Android. Comunica-se com o daemon shell (/data/local/tmp/a52-daemon.sh)
/// através de IPC baseado em arquivos na pasta de dados do app (/storage/emulated/0/Android/data/com.devonno.a52/files).
/// Permite telemetria em tempo real e execução com privilégios UID 2000 (shell).
/// </summary>
public class LocalKernelService : IA52DeviceService
{
    public event EventHandler<TspTelemetry>? TelemetryUpdated;

    private static string GetAppFilesDir()
    {
#if ANDROID
        try
        {
            var dir = Android.App.Application.Context.GetExternalFilesDir(null)?.AbsolutePath;
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return dir;
        }
        catch { }
#endif
        return "/storage/emulated/0/Android/data/com.devonno.a52/files";
    }

    private static int GetIntProp(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number) return v.GetInt32();
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out int n)) return n;
        return 0;
    }

    public async Task<TspTelemetry> PollTelemetryAsync(CancellationToken ct = default)
    {
        string appDir = GetAppFilesDir();
        string telemetryPath = Path.Combine(appDir, "telemetry.json");

        // 1. Tentar ler telemetria atualizada pelo daemon shell
        try
        {
            if (File.Exists(telemetryPath))
            {
                string jsonStr = await File.ReadAllTextAsync(telemetryPath, ct);
                if (!string.IsNullOrWhiteSpace(jsonStr))
                {
                    using var doc = JsonDocument.Parse(jsonStr);
                    var root = doc.RootElement;

                    int tnoi = GetIntProp(root, "tnoi");
                    int tmul = GetIntProp(root, "tmul");
                    int twet = GetIntProp(root, "twet");
                    int slots = GetIntProp(root, "slots");
                    string sensRaw = root.TryGetProperty("sens", out var sensElem) ? sensElem.GetString() ?? "" : "";
                    string scrubRaw = root.TryGetProperty("scrub", out var scrubElem) ? scrubElem.GetString() ?? "" : "";
                    string uptimeRaw = root.TryGetProperty("uptime", out var upElem) ? upElem.GetString() ?? "" : "";

                    // Suporte retrocompatível se vier encapsulado em "hw"
                    if (tnoi == 0 && tmul == 0 && twet == 0 && root.TryGetProperty("hw", out var hwElem))
                    {
                        string hwRaw = hwElem.GetString() ?? "";
                        var matchHw = Regex.Match(hwRaw, @"""TMUL"":""?(\d+)""?.*""TWET"":""?(\d+)""?.*""TNOI"":""?(\d+)""?");
                        if (matchHw.Success)
                        {
                            int.TryParse(matchHw.Groups[1].Value, out tmul);
                            int.TryParse(matchHw.Groups[2].Value, out twet);
                            int.TryParse(matchHw.Groups[3].Value, out tnoi);
                        }
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
                        DeviceSerial = "Galaxy A52 (Daemon Ativo)",
                        Uptime = uptimeRaw.Trim(),
                        Tnoi = tnoi,
                        Tmul = tmul,
                        Twet = twet,
                        ActiveTouchSlots = slots,
                        LastScrubPos = scrubRaw.Trim(),
                        FodNodes = nodes
                    };

                    TelemetryUpdated?.Invoke(this, telemetry);
                    return telemetry;
                }
            }
        }
        catch
        {
            // Erro ao ler JSON do daemon, fallback para sysfs
        }

        // 2. Fallback modo passivo direto sysfs
        try
        {
            string hwRaw = ReadSysfs("/sys/class/sec/tsp/hw_param");
            string sensRaw = ReadSysfs("/sys/class/sec/tsp/sensitivity_mode");
            string scrubRaw = ReadSysfs("/sys/class/sec/tsp/scrub_pos");

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

            var telemetry = new TspTelemetry
            {
                DeviceConnected = true,
                DeviceSerial = "Galaxy A52 (Acesso Kernel)",
                Uptime = "Modo Local Direto",
                Tnoi = tnoi,
                Tmul = tmul,
                Twet = twet,
                ActiveTouchSlots = 0,
                LastScrubPos = scrubRaw.Trim(),
                FodNodes = nodes
            };

            TelemetryUpdated?.Invoke(this, telemetry);
            return telemetry;
        }
        catch
        {
            var telemetry = new TspTelemetry
            {
                DeviceConnected = true,
                DeviceSerial = "Dispositivo (Sem Sensores TSP)",
                Uptime = "Ambiente Genérico / Emulador"
            };
            TelemetryUpdated?.Invoke(this, telemetry);
            return telemetry;
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

    private static async Task<bool> ExecuteDaemonCommandAsync(string command, CancellationToken ct)
    {
        try
        {
            string appDir = GetAppFilesDir();
            if (!Directory.Exists(appDir))
            {
                Directory.CreateDirectory(appDir);
            }

            string cmdPath = Path.Combine(appDir, "cmd");
            string resultPath = Path.Combine(appDir, "cmd_result");

            if (File.Exists(resultPath))
            {
                File.Delete(resultPath);
            }

            await File.WriteAllTextAsync(cmdPath, command, ct);

            // Aguarda resultado retornado pelo daemon por até 3.5 segundos
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 3500)
            {
                if (ct.IsCancellationRequested) break;

                if (File.Exists(resultPath))
                {
                    string res = await File.ReadAllTextAsync(resultPath, ct);
                    if (res.Trim().Equals("OK", StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(resultPath); } catch { }
                        return true;
                    }
                }
                await Task.Delay(100, ct);
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UnlockTouchAsync(CancellationToken ct = default)
        => await ExecuteDaemonCommandAsync("unlock-touch", ct);

    public async Task<bool> CycleDisplayPowerAsync(CancellationToken ct = default)
        => await ExecuteDaemonCommandAsync("cycle-screen", ct);

    public async Task<bool> ApplyHardeningSettingsAsync(CancellationToken ct = default)
        => await ExecuteDaemonCommandAsync("hardening", ct);

    public async Task<bool> RebootDeviceAsync(CancellationToken ct = default)
        => await ExecuteDaemonCommandAsync("reboot", ct);
}
