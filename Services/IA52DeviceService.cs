using A52.DeviceManager.Models;

namespace A52.DeviceManager.Services;

public interface IA52DeviceService
{
    event EventHandler<TspTelemetry>? TelemetryUpdated;
    Task<TspTelemetry> PollTelemetryAsync(CancellationToken ct = default);
    Task<bool> UnlockTouchAsync(CancellationToken ct = default);
    Task<bool> CycleDisplayPowerAsync(CancellationToken ct = default);
    Task<bool> ApplyHardeningSettingsAsync(CancellationToken ct = default);
    Task<bool> RebootDeviceAsync(CancellationToken ct = default);
}
