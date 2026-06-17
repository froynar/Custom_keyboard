using Custom_keyboard.Models.Devices;

namespace Custom_keyboard.Repositories;

public interface IDeviceRepository
{
    // Upsert. Generates a DEV_{Guid:N} id when DeviceId is empty.
    Task<Device> SaveAsync(Device device, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Device>> GetBySellerAsync(int sellerUserId, CancellationToken cancellationToken = default);
    Task<Device?> GetByIdAsync(string deviceId, CancellationToken cancellationToken = default);
}
