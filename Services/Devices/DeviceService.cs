using Custom_keyboard.Models.Devices;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Repositories;

namespace Custom_keyboard.Services.Devices;

// Persist-only QC service. It validates, applies DeviceQcRules, and writes to SQL Server
// (the source of truth). It does NOT depend on IRealtimeNotifier and never publishes/re-publishes
// telemetry — that is the simulator/publisher's job (plan §6.2/§6.3), which avoids publish loops.
public sealed class DeviceService : IDeviceService
{
    private const string DefaultSwitchTechnology = "Mechanical";
    private const string QcStationName = "Keyboard QC Station";

    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceTestSessionRepository _sessionRepository;
    private readonly IDeviceKeyTestResultRepository _keyResultRepository;

    public DeviceService(
        IDeviceRepository deviceRepository,
        IDeviceTestSessionRepository sessionRepository,
        IDeviceKeyTestResultRepository keyResultRepository)
    {
        _deviceRepository = deviceRepository;
        _sessionRepository = sessionRepository;
        _keyResultRepository = keyResultRepository;
    }

    public async Task<Device> GetOrCreateQcStationAsync(int sellerUserId, CancellationToken cancellationToken = default)
    {
        if (sellerUserId <= 0)
        {
            throw new InvalidOperationException("A valid seller is required to create a QC station.");
        }

        var devices = await _deviceRepository.GetBySellerAsync(sellerUserId, cancellationToken);
        var existing = devices.FirstOrDefault(device => device.IsActive && device.DeviceType == DeviceType.QC_STATION);
        if (existing is not null)
        {
            return existing;
        }

        var station = new Device
        {
            SellerUserId = sellerUserId,
            DeviceName = QcStationName,
            DeviceType = DeviceType.QC_STATION,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        return await _deviceRepository.SaveAsync(station, cancellationToken);
    }

    public async Task<DeviceTestSession> StartSessionAsync(
        string requestId,
        int sellerUserId,
        string deviceId,
        string switchTechnology,
        NoiseRequirement noiseRequirement,
        int totalKeys,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new InvalidOperationException("A request id is required to start a QC session.");
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new InvalidOperationException("A device id is required to start a QC session.");
        }

        if (totalKeys <= 0)
        {
            throw new InvalidOperationException("A QC session must cover at least one key.");
        }

        var session = new DeviceTestSession
        {
            RequestId = requestId,
            DeviceId = deviceId,
            SellerUserId = sellerUserId,
            SwitchTechnology = string.IsNullOrWhiteSpace(switchTechnology) ? DefaultSwitchTechnology : switchTechnology,
            NoiseRequirement = noiseRequirement,
            TotalKeys = totalKeys,
            Status = TestSessionStatus.Running,
            StartedAt = DateTime.UtcNow
        };

        // INSERT the Running row first (generates the session id) so per-key FK session_id always holds.
        return await _sessionRepository.SaveAsync(session, cancellationToken);
    }

    public Task<DeviceTestSession> StartSessionAsync(
        string requestId,
        int sellerUserId,
        string deviceId,
        string switchTechnology,
        int totalKeys,
        CancellationToken cancellationToken = default)
        => StartSessionAsync(
            requestId,
            sellerUserId,
            deviceId,
            switchTechnology,
            NoiseRequirement.Normal,
            totalKeys,
            cancellationToken);

    public async Task<DeviceKeyTestResult> RecordKeyResultAsync(KeyTelemetry telemetry, CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(telemetry.SessionId, cancellationToken)
            ?? throw new InvalidOperationException($"QC session '{telemetry.SessionId}' was not found.");

        // Switch/noise/stuck thresholds come from the session (authoritative), not the telemetry,
        // so rule inputs cannot drift across per-key events.
        var thresholds = new QcThresholds(session.SwitchTechnology, session.NoiseRequirement);
        var evaluation = DeviceQcRules.Evaluate(telemetry, thresholds);

        var result = new DeviceKeyTestResult
        {
            SessionId = session.SessionId,
            RequestId = session.RequestId,        // copy from session => FK + denormalization aligned
            DeviceId = session.DeviceId,          // copy from session
            KeyCode = telemetry.KeyCode,
            ExpectedKey = telemetry.ExpectedKey,
            ReceivedKey = telemetry.ReceivedKey,
            PressSignalDetected = telemetry.PressSignalDetected,
            LatencyMs = telemetry.LatencyMs,
            PressEventCount = telemetry.PressEventCount,
            BounceCount = telemetry.BounceCount,
            ReleaseSignalDetected = telemetry.ReleaseSignalDetected,
            HoldDurationMs = telemetry.HoldDurationMs,
            IsStuck = telemetry.IsStuck,
            NoiseDb = telemetry.NoiseDb,
            SwitchTechnology = session.SwitchTechnology,
            Result = evaluation.Result,
            FailureType = evaluation.FailureType,
            FailureReason = evaluation.Reason
        };

        return await _keyResultRepository.InsertAsync(result, cancellationToken);
    }

    public async Task<DeviceTestSession> CompleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"QC session '{sessionId}' was not found.");

        var keyResults = await _keyResultRepository.GetBySessionAsync(sessionId, cancellationToken);
        session.TestedKeys = keyResults.Count;

        // Do not finalize until every key has reported (plan §6.2/§7, FIX #12/#14). Stay Running so
        // an MQTT subscriber that received the summary early can retry once the rows catch up.
        if (keyResults.Count < session.TotalKeys)
        {
            session.Status = TestSessionStatus.Running;
            return await _sessionRepository.SaveAsync(session, cancellationToken);
        }

        session.PassedKeys = keyResults.Count(result => result.Result == KeyTestResult.Pass);
        session.WarningKeys = keyResults.Count(result => result.Result == KeyTestResult.Warning);
        session.FailedKeys = keyResults.Count(result => result.Result == KeyTestResult.Fail);

        var latencies = keyResults.Where(result => result.LatencyMs.HasValue)
            .Select(result => result.LatencyMs!.Value)
            .ToList();
        session.AverageLatencyMs = latencies.Count > 0
            ? Math.Round(latencies.Average(), 2, MidpointRounding.AwayFromZero)
            : null;
        session.MaxLatencyMs = latencies.Count > 0 ? latencies.Max() : null;

        var noises = keyResults.Where(result => result.NoiseDb.HasValue)
            .Select(result => result.NoiseDb!.Value)
            .ToList();
        session.AverageNoiseDb = noises.Count > 0
            ? Math.Round(noises.Average(), 2, MidpointRounding.AwayFromZero)
            : null;
        session.MaxNoiseDb = noises.Count > 0 ? noises.Max() : null;

        // Summary status (guide §7): any Fail => Failed; no Fail but some Warning => Warning; else Passed.
        session.Status = session.FailedKeys > 0
            ? TestSessionStatus.Failed
            : session.WarningKeys > 0
                ? TestSessionStatus.Warning
                : TestSessionStatus.Passed;
        session.CompletedAt = DateTime.UtcNow;

        return await _sessionRepository.SaveAsync(session, cancellationToken);
    }

    public Task<DeviceTestSession?> GetLatestSessionByRequestAsync(string requestId, CancellationToken cancellationToken = default)
        => _sessionRepository.GetLatestByRequestAsync(requestId, cancellationToken);

    public Task<IReadOnlyList<DeviceKeyTestResult>> GetKeyResultsAsync(string sessionId, CancellationToken cancellationToken = default)
        => _keyResultRepository.GetBySessionAsync(sessionId, cancellationToken);
}
