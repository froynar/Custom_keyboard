using Custom_keyboard.Models.Devices;

namespace Custom_keyboard.Realtime.Devices;

// Publish side of the device telemetry transport (mirrors IRealtimeNotifier, but a SEPARATE
// abstraction — not folded into IRealtimeNotifier, plan §6.3). Returns true when the payload
// reached the broker (a subscriber will persist it); false means the caller must fall back to a
// direct DeviceService call so QC data is never lost (plan §7.1, FIX #11/#12).
public interface IDeviceTelemetryPublisher
{
    Task<bool> PublishKeyTestAsync(KeyTelemetry telemetry, CancellationToken cancellationToken = default);
    Task<bool> PublishSessionSummaryAsync(DeviceTestSession session, CancellationToken cancellationToken = default);
}
