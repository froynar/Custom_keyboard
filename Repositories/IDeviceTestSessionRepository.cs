using Custom_keyboard.Models.Devices;

namespace Custom_keyboard.Repositories;

public interface IDeviceTestSessionRepository
{
    // Upsert: used both for the initial Running INSERT (StartSession) and the
    // aggregated UPDATE (CompleteSession). Generates a QCSESS_{Guid:N} id when SessionId is empty.
    Task<DeviceTestSession> SaveAsync(DeviceTestSession session, CancellationToken cancellationToken = default);
    Task<DeviceTestSession?> GetLatestByRequestAsync(string requestId, CancellationToken cancellationToken = default);
    Task<DeviceTestSession?> GetByIdAsync(string sessionId, CancellationToken cancellationToken = default);
}
