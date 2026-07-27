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
    private const int MaxQcKeys = 256;
    private const int MaxPressEvents = 100;
    private const int MaxHoldDurationMs = 3_600_000;
    private const decimal MaxLatencyMs = 10_000m;
    private const decimal MaxNoiseDb = 200m;

    private readonly IDeviceRepository _deviceRepository;
    private readonly IRequestRepository _requestRepository;
    private readonly IDeviceTestSessionRepository _sessionRepository;
    private readonly IDeviceKeyTestResultRepository _keyResultRepository;

    public DeviceService(
        IDeviceRepository deviceRepository,
        IRequestRepository requestRepository,
        IDeviceTestSessionRepository sessionRepository,
        IDeviceKeyTestResultRepository keyResultRepository)
    {
        _deviceRepository = deviceRepository;
        _requestRepository = requestRepository;
        _sessionRepository = sessionRepository;
        _keyResultRepository = keyResultRepository;
    }

    public async Task<Device> GetOrCreateQcStationAsync(int sellerUserId, CancellationToken cancellationToken = default)
    {
        if (sellerUserId <= 0)
        {
            throw new InvalidOperationException("A valid seller is required to create a QC station.");
        }

        return await _deviceRepository.GetOrCreateActiveQcStationAsync(
            sellerUserId,
            QcStationName,
            cancellationToken);
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

        if (sellerUserId <= 0)
        {
            throw new InvalidOperationException("A valid seller is required to start a QC session.");
        }

        if (totalKeys is <= 0 or > MaxQcKeys)
        {
            throw new InvalidOperationException($"A QC session must cover between 1 and {MaxQcKeys} keys.");
        }

        var request = await _requestRepository.GetByIdAsync(requestId, cancellationToken)
            ?? throw new InvalidOperationException($"Build request '{requestId}' was not found.");
        if (request.SellerUserId != sellerUserId)
        {
            throw new InvalidOperationException("The QC request does not belong to the selected seller.");
        }

        if (request.Status != RequestStatus.In_progress)
        {
            throw new InvalidOperationException("QC can only start for a request that is in progress.");
        }

        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken)
            ?? throw new InvalidOperationException($"QC device '{deviceId}' was not found.");
        if (device.SellerUserId != sellerUserId)
        {
            throw new InvalidOperationException("The QC device does not belong to the request seller.");
        }

        if (!device.IsActive || device.DeviceType != DeviceType.QC_STATION)
        {
            throw new InvalidOperationException("The selected device is not an active QC station.");
        }

        var normalizedSwitchTechnology = NormalizeSwitchTechnology(switchTechnology);
        var runningSession = await _sessionRepository.GetLatestByRequestAsync(requestId, cancellationToken);
        if (runningSession?.Status == TestSessionStatus.Running)
        {
            if (string.Equals(runningSession.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    runningSession.SwitchTechnology,
                    normalizedSwitchTechnology,
                    StringComparison.OrdinalIgnoreCase)
                && runningSession.NoiseRequirement == noiseRequirement
                && runningSession.TotalKeys == totalKeys)
            {
                return runningSession;
            }

            throw new InvalidOperationException("This request already has a different QC session running.");
        }

        var session = new DeviceTestSession
        {
            RequestId = requestId,
            DeviceId = deviceId,
            SwitchTechnology = normalizedSwitchTechnology,
            NoiseRequirement = noiseRequirement,
            TotalKeys = totalKeys,
            Status = TestSessionStatus.Running,
            CompletedAt = null
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
        ArgumentNullException.ThrowIfNull(telemetry);
        ValidateTelemetryShape(telemetry);

        var session = await _sessionRepository.GetByIdAsync(telemetry.SessionId, cancellationToken)
            ?? throw new InvalidOperationException($"QC session '{telemetry.SessionId}' was not found.");

        if (session.Status != TestSessionStatus.Running)
        {
            throw new InvalidOperationException("The QC session is already finalized.");
        }

        if (!string.Equals(session.RequestId, telemetry.RequestId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(session.DeviceId, telemetry.DeviceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Telemetry request/device identity does not match its QC session.");
        }

        if (!string.IsNullOrWhiteSpace(telemetry.SwitchTechnology)
            && !string.Equals(
                session.SwitchTechnology,
                telemetry.SwitchTechnology.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Telemetry switch technology does not match its QC session.");
        }

        // Switch/noise/stuck thresholds come from the session (authoritative), not the telemetry,
        // so rule inputs cannot drift across per-key events.
        var thresholds = new QcThresholds(session.SwitchTechnology, session.NoiseRequirement);
        var canonicalTelemetry = CreateCanonicalTelemetry(session, telemetry, thresholds);
        var evaluation = DeviceQcRules.Evaluate(canonicalTelemetry, thresholds);

        var result = new DeviceKeyTestResult
        {
            SessionId = session.SessionId,
            KeyCode = canonicalTelemetry.KeyCode,
            ReceivedKey = canonicalTelemetry.ReceivedKey,
            PressSignalDetected = canonicalTelemetry.PressSignalDetected,
            Latency = canonicalTelemetry.LatencyMs,
            PressCount = canonicalTelemetry.PressEventCount,
            ReleaseSignal = canonicalTelemetry.ReleaseSignalDetected,
            HoldDuration = canonicalTelemetry.HoldDurationMs,
            Noise = canonicalTelemetry.NoiseDb,
            Result = evaluation.Result,
            FailureType = evaluation.FailureType,
            FailureReason = evaluation.Reason
        };

        var saved = await _keyResultRepository.InsertAsync(result, cancellationToken);
        ApplyTransientDiagnostics(saved, session);
        return saved;
    }

    public async Task<DeviceTestSession> CompleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"QC session '{sessionId}' was not found.");

        if (session.Status != TestSessionStatus.Running)
        {
            return session;
        }

        var keyResults = await _keyResultRepository.GetBySessionAsync(sessionId, cancellationToken);
        session.TestedKeys = keyResults.Count;

        // Do not finalize until every key has reported (plan §6.2/§7, FIX #12/#14). Stay Running so
        // an MQTT subscriber that received the summary early can retry once the rows catch up.
        if (keyResults.Count < session.TotalKeys)
        {
            session.Status = TestSessionStatus.Running;
            session.CompletedAt = null;
            return await _sessionRepository.SaveAsync(session, cancellationToken);
        }

        if (keyResults.Count > session.TotalKeys)
        {
            throw new InvalidOperationException("The QC session contains more key results than its declared key count.");
        }

        session.PassedKeys = keyResults.Count(result => result.Result == KeyTestResult.Pass);
        session.WarningKeys = keyResults.Count(result => result.Result == KeyTestResult.Warning);
        session.FailedKeys = keyResults.Count(result => result.Result == KeyTestResult.Fail);

        var latencies = keyResults.Where(result => result.Latency.HasValue)
            .Select(result => result.Latency!.Value)
            .ToList();
        session.AverageLatencyMs = latencies.Count > 0
            ? Math.Round(latencies.Average(), 2, MidpointRounding.AwayFromZero)
            : null;
        session.MaxLatencyMs = latencies.Count > 0 ? latencies.Max() : null;

        var noises = keyResults.Where(result => result.Noise.HasValue)
            .Select(result => result.Noise!.Value)
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

    public async Task<IReadOnlyList<DeviceKeyTestResult>> GetKeyResultsAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"QC session '{sessionId}' was not found.");
        var results = await _keyResultRepository.GetBySessionAsync(sessionId, cancellationToken);
        var thresholds = new QcThresholds(session.SwitchTechnology, session.NoiseRequirement);

        foreach (var result in results)
        {
            ApplyTransientDiagnostics(result, session, thresholds);
        }

        return results;
    }

    private static string NormalizeSwitchTechnology(string? switchTechnology)
    {
        var normalized = string.IsNullOrWhiteSpace(switchTechnology)
            ? DefaultSwitchTechnology
            : switchTechnology.Trim();

        if (!string.Equals(normalized, "Mechanical", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalized, "HE", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported switch technology '{normalized}'.");
        }

        return string.Equals(normalized, "HE", StringComparison.OrdinalIgnoreCase)
            ? "HE"
            : DefaultSwitchTechnology;
    }

    private static void ValidateTelemetryShape(KeyTelemetry telemetry)
    {
        if (string.IsNullOrWhiteSpace(telemetry.SessionId)
            || string.IsNullOrWhiteSpace(telemetry.RequestId)
            || string.IsNullOrWhiteSpace(telemetry.DeviceId))
        {
            throw new InvalidOperationException("Telemetry must identify its session, request, and device.");
        }

        if (string.IsNullOrWhiteSpace(telemetry.KeyCode) || telemetry.KeyCode.Length > 30)
        {
            throw new InvalidOperationException("Telemetry key code is missing or too long.");
        }

        if (!string.Equals(telemetry.KeyCode, telemetry.ExpectedKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Telemetry expected key must match its key code.");
        }

        if (telemetry.ReceivedKey is { Length: > 30 })
        {
            throw new InvalidOperationException("Telemetry received key is too long.");
        }

        if (telemetry.PressEventCount is < 0 or > MaxPressEvents
            || telemetry.LatencyMs is < 0 or > MaxLatencyMs
            || telemetry.NoiseDb is < 0 or > MaxNoiseDb
            || telemetry.HoldDurationMs is < 0 or > MaxHoldDurationMs)
        {
            throw new InvalidOperationException("Telemetry contains a measurement outside the accepted range.");
        }

        if (!telemetry.PressSignalDetected
            && (telemetry.PressEventCount != 0
                || telemetry.ReleaseSignalDetected
                || telemetry.LatencyMs is not null
                || telemetry.HoldDurationMs is not null))
        {
            throw new InvalidOperationException("No-signal telemetry contains contradictory press data.");
        }

        if (telemetry.PressSignalDetected
            && (telemetry.PressEventCount == 0
                || string.IsNullOrWhiteSpace(telemetry.ReceivedKey)
                || telemetry.LatencyMs is null
                || telemetry.HoldDurationMs is null))
        {
            throw new InvalidOperationException("Detected presses require a key, latency, hold duration, and press count.");
        }
    }

    private static KeyTelemetry CreateCanonicalTelemetry(
        DeviceTestSession session,
        KeyTelemetry telemetry,
        QcThresholds thresholds)
    {
        int? bounceCount = telemetry.ReleaseSignalDetected
            ? Math.Max(0, telemetry.PressEventCount - 1)
            : null;
        var isStuck = telemetry.PressSignalDetected
            && (!telemetry.ReleaseSignalDetected
                || telemetry.HoldDurationMs is int hold && hold > thresholds.StuckThresholdMs);

        if (telemetry.BounceCount != bounceCount || telemetry.IsStuck != isStuck)
        {
            throw new InvalidOperationException("Telemetry bounce/stuck flags do not match the persisted signal fields.");
        }

        return new KeyTelemetry
        {
            SessionId = session.SessionId,
            RequestId = session.RequestId,
            DeviceId = session.DeviceId,
            KeyCode = telemetry.KeyCode.Trim(),
            ExpectedKey = telemetry.KeyCode.Trim(),
            ReceivedKey = telemetry.ReceivedKey?.Trim(),
            PressSignalDetected = telemetry.PressSignalDetected,
            LatencyMs = telemetry.LatencyMs,
            PressEventCount = telemetry.PressEventCount,
            BounceCount = bounceCount,
            ReleaseSignalDetected = telemetry.ReleaseSignalDetected,
            HoldDurationMs = telemetry.HoldDurationMs,
            IsStuck = isStuck,
            NoiseDb = telemetry.NoiseDb,
            SwitchTechnology = session.SwitchTechnology
        };
    }

    private static void ApplyTransientDiagnostics(
        DeviceKeyTestResult result,
        DeviceTestSession session,
        QcThresholds? thresholds = null)
    {
        thresholds ??= new QcThresholds(session.SwitchTechnology, session.NoiseRequirement);
        var telemetry = new KeyTelemetry
        {
            SessionId = session.SessionId,
            RequestId = session.RequestId,
            DeviceId = session.DeviceId,
            KeyCode = result.KeyCode,
            ExpectedKey = result.KeyCode,
            ReceivedKey = result.ReceivedKey,
            PressSignalDetected = result.PressSignalDetected,
            LatencyMs = result.Latency,
            PressEventCount = result.PressCount,
            BounceCount = result.ReleaseSignal ? Math.Max(0, result.PressCount - 1) : null,
            ReleaseSignalDetected = result.ReleaseSignal,
            HoldDurationMs = result.HoldDuration,
            IsStuck = result.PressSignalDetected
                && (!result.ReleaseSignal
                    || result.HoldDuration is int hold && hold > thresholds.StuckThresholdMs),
            NoiseDb = result.Noise,
            SwitchTechnology = session.SwitchTechnology
        };

        var evaluation = DeviceQcRules.Evaluate(telemetry, thresholds);
        result.BounceCount = telemetry.BounceCount;
        result.IsStuck = telemetry.IsStuck;
        result.FailureType = evaluation.FailureType;
        result.FailureReason = evaluation.Reason;
    }
}
