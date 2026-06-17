using Custom_keyboard.Models.Devices;

namespace Custom_keyboard.Realtime.Devices;

// Used when MQTT is disabled or no broker is configured. Always returns false so the simulator
// records QC results directly through DeviceService — data is never lost (plan §7.1, FIX #11/#12).
public sealed class NullDeviceTelemetryPublisher : IDeviceTelemetryPublisher
{
    public Task<bool> PublishKeyTestAsync(KeyTelemetry telemetry, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> PublishSessionSummaryAsync(DeviceTestSession session, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
