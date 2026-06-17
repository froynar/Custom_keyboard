using Custom_keyboard.Models.Devices;
using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Services.Devices;

public interface IDeviceService
{
    // Ensures the seller has an active QC_STATION device (created on demand, plan §3.3).
    Task<Device> GetOrCreateQcStationAsync(int sellerUserId, CancellationToken cancellationToken = default);

    // Step 1: create the session row (Running) BEFORE any key result, so FK session_id always holds.
    Task<DeviceTestSession> StartSessionAsync(
        string requestId,
        int sellerUserId,
        string deviceId,
        string switchTechnology,
        NoiseRequirement noiseRequirement,
        int totalKeys,
        CancellationToken cancellationToken = default);

    // Step 2 (N times): evaluate one key's telemetry and persist the per-key result.
    Task<DeviceKeyTestResult> RecordKeyResultAsync(KeyTelemetry telemetry, CancellationToken cancellationToken = default);

    // Step 3: aggregate + finalize. Stays Running (does not finalize) until all total_keys are in (plan §6.2/§7).
    Task<DeviceTestSession> CompleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    // Read helpers for the dashboards (plan §9).
    Task<DeviceTestSession?> GetLatestSessionByRequestAsync(string requestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeviceKeyTestResult>> GetKeyResultsAsync(string sessionId, CancellationToken cancellationToken = default);
}
