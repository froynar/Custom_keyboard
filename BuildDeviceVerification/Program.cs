using Custom_keyboard.Data.SqlServer;
using Custom_keyboard.Models.Builds;
using Custom_keyboard.Models.Devices;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Repositories;
using Custom_keyboard.Repositories.SqlServer;
using Custom_keyboard.Realtime.Devices;
using Custom_keyboard.Services.Devices;

var passed = 0;

Run("clean mechanical key passes", () =>
{
    var evaluation = DeviceQcRules.Evaluate(
        Telemetry("A"),
        new QcThresholds("Mechanical", NoiseRequirement.Normal));
    Equal(KeyTestResult.Pass, evaluation.Result);
});

Run("chatter is detected", () =>
{
    var telemetry = Telemetry("B");
    telemetry.PressEventCount = 2;
    telemetry.BounceCount = 1;
    var evaluation = DeviceQcRules.Evaluate(telemetry, new QcThresholds("Mechanical"));
    Equal(KeyTestResult.Fail, evaluation.Result);
    Equal(KeyFailureType.Chatter, evaluation.FailureType);
});

Run("stuck key is detected", () =>
{
    var telemetry = Telemetry("C");
    telemetry.ReleaseSignalDetected = false;
    telemetry.BounceCount = null;
    telemetry.HoldDurationMs = 1500;
    telemetry.IsStuck = true;
    var evaluation = DeviceQcRules.Evaluate(telemetry, new QcThresholds("Mechanical"));
    Equal(KeyFailureType.StuckKey, evaluation.FailureType);
});

Run("MQTT topic parser binds device and request identity", () =>
{
    Equal(
        true,
        DeviceTelemetryTransport.TryParseKeyTestTopic(
            "keyboard",
            "keyboard/device/DEV_VERIFY/request/REQ_VERIFY/key-test",
            out var deviceId,
            out var requestId));
    Equal("DEV_VERIFY", deviceId);
    Equal("REQ_VERIFY", requestId);
    Equal(
        false,
        DeviceTelemetryTransport.TryParseKeyTestTopic(
            "keyboard",
            "other/device/DEV_VERIFY/request/REQ_VERIFY/key-test",
            out _,
            out _));
});

Run("MQTT device telemetry rejects unsigned payload changes", () =>
{
    const string secret = "verification-secret";
    var signed = DeviceTelemetryTransport.SerializeSigned(Telemetry("A"), secret);
    var decoded = DeviceTelemetryTransport.DeserializeSigned<KeyTelemetry>(signed, secret);
    Equal("A", decoded.KeyCode);

    var tampered = signed.Replace("DEV_VERIFY", "DEV_TAMPER", StringComparison.Ordinal);
    var rejected = false;
    try
    {
        _ = DeviceTelemetryTransport.DeserializeSigned<KeyTelemetry>(tampered, secret);
    }
    catch (InvalidOperationException)
    {
        rejected = true;
    }

    Equal(true, rejected);
});

await RunAsync("device lifecycle and transient QC projections", async () =>
{
    var requestRepository = new StubRequestRepository
    {
        Request = new BuildRequest
        {
            RequestId = "REQ_VERIFY",
            SellerUserId = 7,
            Status = RequestStatus.In_progress
        }
    };
    var deviceRepository = new StubDeviceRepository
    {
        Device = new Device
        {
            DeviceId = "DEV_VERIFY",
            SellerUserId = 7,
            DeviceName = "Verifier",
            DeviceType = DeviceType.QC_STATION,
            IsActive = true
        }
    };
    var sessionRepository = new StubSessionRepository();
    var keyRepository = new StubKeyResultRepository();
    var service = new DeviceService(
        deviceRepository,
        requestRepository,
        sessionRepository,
        keyRepository);

    var session = await service.StartSessionAsync(
        "REQ_VERIFY",
        7,
        "DEV_VERIFY",
        "Mechanical",
        NoiseRequirement.Normal,
        2);

    var first = Telemetry("A", session);
    var savedFirst = await service.RecordKeyResultAsync(first);
    Equal(0, savedFirst.BounceCount);
    Equal(false, savedFirst.IsStuck);
    Equal(KeyTestResult.Pass, savedFirst.Result);

    var second = Telemetry("B", session);
    second.PressEventCount = 2;
    second.BounceCount = 1;
    var savedSecond = await service.RecordKeyResultAsync(second);
    Equal(1, savedSecond.BounceCount);
    Equal(KeyFailureType.Chatter, savedSecond.FailureType);

    var completed = await service.CompleteSessionAsync(session.SessionId);
    Equal(TestSessionStatus.Failed, completed.Status);
    Equal(2, completed.TestedKeys);
});

await RunAsync("device service rejects cross-session telemetry", async () =>
{
    var requestRepository = new StubRequestRepository
    {
        Request = new BuildRequest
        {
            RequestId = "REQ_VERIFY",
            SellerUserId = 7,
            Status = RequestStatus.In_progress
        }
    };
    var deviceRepository = new StubDeviceRepository
    {
        Device = new Device
        {
            DeviceId = "DEV_VERIFY",
            SellerUserId = 7,
            DeviceName = "Verifier",
            DeviceType = DeviceType.QC_STATION,
            IsActive = true
        }
    };
    var sessionRepository = new StubSessionRepository();
    var service = new DeviceService(
        deviceRepository,
        requestRepository,
        sessionRepository,
        new StubKeyResultRepository());
    var session = await service.StartSessionAsync(
        "REQ_VERIFY",
        7,
        "DEV_VERIFY",
        "Mechanical",
        NoiseRequirement.Normal,
        1);
    var telemetry = Telemetry("A", session);
    telemetry.DeviceId = "DEV_OTHER";

    await ThrowsAsync<InvalidOperationException>(() => service.RecordKeyResultAsync(telemetry));
});

await RunAsync("SQL Server schema guard accepts the hardened clone", async () =>
{
    await new SqlServerHealthCheck(new SqlConnectionFactory()).EnsureCompatibleSchemaAsync();
});

await RunAsync("guarded request transition is race-safe and non-mutating on stale state", async () =>
{
    var repository = new SqlRequestRepository(new SqlConnectionFactory());
    var request = await repository.GetByIdAsync("REQ_8afe9c8f50b84c7f9e477b2999bb598b")
        ?? throw new InvalidOperationException("Verification request was not found.");
    var actualStatus = request.Status;
    var staleExpectedStatus = actualStatus == RequestStatus.Pending
        ? RequestStatus.Completed
        : RequestStatus.Pending;

    var result = await repository.TryUpdateStatusAsync(
        request,
        staleExpectedStatus,
        requireAcceptableQc: false);
    Equal<BuildRequest?>(null, result);

    var unchanged = await repository.GetByIdAsync(request.RequestId)
        ?? throw new InvalidOperationException("Verification request disappeared.");
    Equal(actualStatus, unchanged.Status);
});

await RunAsync("persisted QC rows reconstruct Bounce and Stuck without extra DB columns", async () =>
{
    var connectionFactory = new SqlConnectionFactory();
    var service = new DeviceService(
        new SqlDeviceRepository(connectionFactory),
        new SqlRequestRepository(connectionFactory),
        new SqlDeviceTestSessionRepository(connectionFactory),
        new SqlDeviceKeyTestResultRepository(connectionFactory));
    var session = await service.GetLatestSessionByRequestAsync(
        "REQ_8afe9c8f50b84c7f9e477b2999bb598b")
        ?? throw new InvalidOperationException("Persisted verification QC session was not found.");
    var results = await service.GetKeyResultsAsync(session.SessionId);

    Equal(85, results.Count);
    Equal(
        true,
        results.All(result =>
            result.BounceCount == (result.ReleaseSignal ? Math.Max(0, result.PressCount - 1) : null)));
});

Console.WriteLine($"Build/device verification passed: {passed} checks.");
return;

void Run(string name, Action test)
{
    test();
    passed++;
    Console.WriteLine($"PASS {name}");
}

async Task RunAsync(string name, Func<Task> test)
{
    await test();
    passed++;
    Console.WriteLine($"PASS {name}");
}

static KeyTelemetry Telemetry(string keyCode, DeviceTestSession? session = null)
    => new()
    {
        SessionId = session?.SessionId ?? "QC_VERIFY",
        RequestId = session?.RequestId ?? "REQ_VERIFY",
        DeviceId = session?.DeviceId ?? "DEV_VERIFY",
        KeyCode = keyCode,
        ExpectedKey = keyCode,
        ReceivedKey = keyCode,
        PressSignalDetected = true,
        LatencyMs = 5m,
        PressEventCount = 1,
        BounceCount = 0,
        ReleaseSignalDetected = true,
        HoldDurationMs = 90,
        IsStuck = false,
        NoiseDb = 40m,
        SwitchTechnology = "Mechanical"
    };

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}

static async Task ThrowsAsync<TException>(Func<Task> action)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

file sealed class StubDeviceRepository : IDeviceRepository
{
    public required Device Device { get; init; }

    public Task<Device> SaveAsync(Device device, CancellationToken cancellationToken = default)
        => Task.FromResult(device);

    public Task<Device> GetOrCreateActiveQcStationAsync(
        int sellerUserId,
        string deviceName,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Device);

    public Task<IReadOnlyList<Device>> GetBySellerAsync(
        int sellerUserId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Device>>([Device]);

    public Task<Device?> GetByIdAsync(string deviceId, CancellationToken cancellationToken = default)
        => Task.FromResult<Device?>(
            string.Equals(deviceId, Device.DeviceId, StringComparison.OrdinalIgnoreCase) ? Device : null);
}

file sealed class StubRequestRepository : IRequestRepository
{
    public required BuildRequest Request { get; init; }

    public Task<BuildRequest?> GetByIdAsync(string requestId, CancellationToken cancellationToken = default)
        => Task.FromResult<BuildRequest?>(
            string.Equals(requestId, Request.RequestId, StringComparison.OrdinalIgnoreCase) ? Request : null);

    public Task<IReadOnlyList<BuildRequest>> GetByBuyerAsync(
        int buyerId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<BuildRequest>>([Request]);

    public Task<IReadOnlyList<BuildRequest>> GetBySellerAsync(
        int sellerUserId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<BuildRequest>>([Request]);

    public Task<int> GetCountAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(1);

    public Task<BuildRequest> SaveAsync(BuildRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(request);

    public Task<BuildRequest?> TryUpdateStatusAsync(
        BuildRequest request,
        RequestStatus expectedStatus,
        bool requireAcceptableQc,
        CancellationToken cancellationToken = default)
        => Task.FromResult<BuildRequest?>(request);

    public Task<BuildRequest> SaveAndSetBuildStatusAsync(
        BuildRequest request,
        BuildStatus buildStatus,
        CancellationToken cancellationToken = default)
        => Task.FromResult(request);
}

file sealed class StubSessionRepository : IDeviceTestSessionRepository
{
    private DeviceTestSession? _session;

    public Task<DeviceTestSession> SaveAsync(
        DeviceTestSession session,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(session.SessionId))
        {
            session.SessionId = "QC_VERIFY";
        }

        _session = session;
        return Task.FromResult(session);
    }

    public Task<DeviceTestSession?> GetLatestByRequestAsync(
        string requestId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(
            string.Equals(_session?.RequestId, requestId, StringComparison.OrdinalIgnoreCase)
                ? _session
                : null);

    public Task<DeviceTestSession?> GetByIdAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(
            string.Equals(_session?.SessionId, sessionId, StringComparison.OrdinalIgnoreCase)
                ? _session
                : null);
}

file sealed class StubKeyResultRepository : IDeviceKeyTestResultRepository
{
    private readonly List<DeviceKeyTestResult> _results = [];

    public Task<DeviceKeyTestResult> InsertAsync(
        DeviceKeyTestResult result,
        CancellationToken cancellationToken = default)
    {
        var existing = _results.FirstOrDefault(item =>
            string.Equals(item.SessionId, result.SessionId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.KeyCode, result.KeyCode, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return Task.FromResult(existing);
        }

        result.KeyTestId = _results.Count + 1;
        result.RecordedAt = DateTime.UtcNow;
        _results.Add(result);
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<DeviceKeyTestResult>> GetBySessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DeviceKeyTestResult>>(
            _results.Where(item =>
                string.Equals(item.SessionId, sessionId, StringComparison.OrdinalIgnoreCase)).ToList());
}
