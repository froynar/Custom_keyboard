using Custom_keyboard.Models.Devices;

namespace Custom_keyboard.Repositories;

public interface IDeviceKeyTestResultRepository
{
    // INSERT only (key_test_id is IDENTITY). The caller supplies request_id/device_id
    // copied from the owning session so the per-key FKs stay aligned.
    Task<DeviceKeyTestResult> InsertAsync(DeviceKeyTestResult result, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeviceKeyTestResult>> GetBySessionAsync(string sessionId, CancellationToken cancellationToken = default);
}
