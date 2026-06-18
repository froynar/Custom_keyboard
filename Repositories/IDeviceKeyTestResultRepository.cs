using Custom_keyboard.Models.Devices;

namespace Custom_keyboard.Repositories;

public interface IDeviceKeyTestResultRepository
{
    // Idempotent per (session_id, key_code): MQTT QoS/fallback may deliver the same
    // telemetry more than once, but one physical key should count once per session.
    Task<DeviceKeyTestResult> InsertAsync(DeviceKeyTestResult result, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeviceKeyTestResult>> GetBySessionAsync(string sessionId, CancellationToken cancellationToken = default);
}
