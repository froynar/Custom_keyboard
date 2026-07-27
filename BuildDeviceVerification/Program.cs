using System.Text.Json;
using Custom_keyboard.Data.SqlServer;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Admin;
using Custom_keyboard.Models.Builds;
using Custom_keyboard.Models.Components;
using Custom_keyboard.Models.Devices;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Repositories;
using Custom_keyboard.Repositories.SqlServer;
using Custom_keyboard.Realtime;
using Custom_keyboard.Realtime.Devices;
using Custom_keyboard.Services;
using Custom_keyboard.Services.Devices;
using Custom_keyboard.Services.Stats;

const string VerificationDatabase = "CKDB_Verification";
var configuredDatabase = new SqlServerSettings().Database;
if (!string.Equals(configuredDatabase, VerificationDatabase, StringComparison.Ordinal))
{
    throw new InvalidOperationException(
        $"BuildDeviceVerification must run against the isolated '{VerificationDatabase}' database. "
        + "Set CUSTOM_KEYBOARD_DATABASE before starting the verification program.");
}

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

Run("MQTT stays opt-in for DB-only installations", () =>
{
    var previous = Environment.GetEnvironmentVariable("CUSTOM_KEYBOARD_MQTT_ENABLED");
    try
    {
        Environment.SetEnvironmentVariable("CUSTOM_KEYBOARD_MQTT_ENABLED", null);
        Equal(false, new MqttSettings().Enabled);

        Environment.SetEnvironmentVariable("CUSTOM_KEYBOARD_MQTT_ENABLED", "true");
        Equal(true, new MqttSettings().Enabled);

        Environment.SetEnvironmentVariable("CUSTOM_KEYBOARD_MQTT_ENABLED", "0");
        Equal(false, new MqttSettings().Enabled);
    }
    finally
    {
        Environment.SetEnvironmentVariable("CUSTOM_KEYBOARD_MQTT_ENABLED", previous);
    }
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

    var stillRunning = await service.CompleteSessionAsync(session.SessionId);
    Equal(TestSessionStatus.Running, stillRunning.Status);
    Equal<DateTime?>(null, stillRunning.CompletedAt);

    var second = Telemetry("B", session);
    second.PressEventCount = 2;
    second.BounceCount = 1;
    var savedSecond = await service.RecordKeyResultAsync(second);
    Equal(1, savedSecond.BounceCount);
    Equal(KeyFailureType.Chatter, savedSecond.FailureType);

    var completed = await service.CompleteSessionAsync(session.SessionId);
    Equal(TestSessionStatus.Failed, completed.Status);
    Equal(2, completed.TestedKeys);
    Equal(true, completed.CompletedAt.HasValue);
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

await RunAsync("request snapshots prefer persisted projections and preserve historical prices", async () =>
{
    var projectedCatalog = SnapshotCatalog();
    var projectedBuild = SnapshotBuild(usePersistedProjection: true);
    var projectedRequest = await SendSnapshotRequestAsync(projectedBuild, projectedCatalog);
    using (var payload = JsonDocument.Parse(projectedRequest.RequestPayloadJson))
    {
        var item = payload.RootElement.GetProperty("items")[0];
        Equal("Projected switch", item.GetProperty("productName").GetString());
        Equal(5.25m, item.GetProperty("unitPriceSnapshot").GetDecimal());
        Equal(10.50m, item.GetProperty("lineTotal").GetDecimal());
    }

    // One lookup is required by build validation; snapshot creation must reuse Build_items.
    Equal(1, projectedCatalog.SwitchByIdCalls);

    var transientCatalog = SnapshotCatalog();
    var transientBuild = SnapshotBuild(usePersistedProjection: false);
    var transientRequest = await SendSnapshotRequestAsync(transientBuild, transientCatalog);
    using (var payload = JsonDocument.Parse(transientRequest.RequestPayloadJson))
    {
        var item = payload.RootElement.GetProperty("items")[0];
        Equal("Current catalog switch", item.GetProperty("productName").GetString());
        Equal(5.25m, item.GetProperty("unitPriceSnapshot").GetDecimal());
        Equal(10.50m, item.GetProperty("lineTotal").GetDecimal());
    }

    // A transient item has no view projection, so snapshot creation performs the fallback lookup.
    Equal(2, transientCatalog.SwitchByIdCalls);
});

await RunAsync("SQL Server schema guard accepts the hardened clone", async () =>
{
    await new SqlServerHealthCheck(new SqlConnectionFactory()).EnsureCompatibleSchemaAsync();
});

await RunAsync("request projection resolves the seller shop name", async () =>
{
    var repository = new SqlRequestRepository(new SqlConnectionFactory());
    var request = await repository.GetByIdAsync("REQ_8afe9c8f50b84c7f9e477b2999bb598b")
        ?? throw new InvalidOperationException("Verification request was not found.");
    Equal("Soigear Refactor Shop", request.SellerShopName);
});

await RunAsync("catalog, build items, and analytics read through the new views", async () =>
{
    var connectionFactory = new SqlConnectionFactory();
    var requestRepository = new SqlRequestRepository(connectionFactory);
    var request = await requestRepository.GetByIdAsync("REQ_8afe9c8f50b84c7f9e477b2999bb598b")
        ?? throw new InvalidOperationException("Verification request was not found.");

    var build = await new SqlBuildRepository(connectionFactory).GetByIdAsync(request.BuildId)
        ?? throw new InvalidOperationException("Verification build was not found.");
    Equal(true, build.Items.Count > 0);

    var catalog = new SqlComponentRepository(connectionFactory);
    Equal(true, (await catalog.GetAvailableKitsAsync()).Count > 0);
    Equal(true, (await catalog.GetAvailableSwitchesAsync()).Count > 0);
    Equal(true, (await catalog.GetAvailableKeycapSetsAsync()).Count > 0);
    Equal(true, (await catalog.GetAvailableStabilizersAsync()).Count > 0);
    Equal(true, (await catalog.GetAvailableAccessoriesAsync()).Count > 0);

    foreach (var componentType in Enum.GetValues<AdminComponentType>())
    {
        Equal(true, (await catalog.GetAdminComponentsAsync(componentType)).Count > 0);
    }

    var overview = await new SqlStatsRepository(connectionFactory)
        .GetAdminOverviewAsync(StatsPeriod.Monthly);
    Equal(true, overview.TotalRequests > 0);
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

await RunAsync("guarded completion rejects a failed latest QC session", async () =>
{
    var repository = new SqlRequestRepository(new SqlConnectionFactory());
    var request = await repository.GetByIdAsync("REQ_8afe9c8f50b84c7f9e477b2999bb598b")
        ?? throw new InvalidOperationException("Verification request was not found.");
    Equal(RequestStatus.In_progress, request.Status);

    request.Status = RequestStatus.Completed;
    request.CompletedAt = DateTime.UtcNow;
    var result = await repository.TryUpdateStatusAsync(
        request,
        RequestStatus.In_progress,
        requireAcceptableQc: true);
    Equal<BuildRequest?>(null, result);

    var unchanged = await repository.GetByIdAsync(request.RequestId)
        ?? throw new InvalidOperationException("Verification request disappeared.");
    Equal(RequestStatus.In_progress, unchanged.Status);
    Equal<DateTime?>(null, unchanged.CompletedAt);
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

static KeyboardBuild SnapshotBuild(bool usePersistedProjection)
{
    var item = new BuildItem
    {
        BuildItemId = usePersistedProjection ? 91 : 0,
        BuildId = "BUILD_SNAPSHOT",
        SwitchId = "SW_SNAPSHOT",
        Quantity = 2,
        UnitPriceSnapshot = 5.25m
    };

    if (usePersistedProjection)
    {
        item.ComponentType = "Switch";
        item.ComponentId = "SW_SNAPSHOT";
        item.ComponentName = "Projected switch";
        item.LineTotalSnapshot = 10.50m;
        item.CurrentPriceUsd = 99m;
        item.IsAvailable = true;
    }

    return new KeyboardBuild
    {
        BuildId = "BUILD_SNAPSHOT",
        BuyerId = 17,
        KitId = "KIT_SNAPSHOT",
        Name = "Snapshot build",
        Status = BuildStatus.Saved,
        CreatedAt = DateTime.UtcNow,
        Items = [item]
    };
}

static StubSnapshotCatalog SnapshotCatalog()
    => new()
    {
        Kit = new KeyboardKit
        {
            KitId = "KIT_SNAPSHOT",
            BrandId = 1,
            LayoutId = "LAYOUT_SNAPSHOT",
            KitName = "Snapshot kit",
            PcbTechnology = "Mechanical",
            SwitchMount = "MX 5-pin",
            RequiredSwitchQuantity = 2,
            PriceUsd = 100m,
            IsAvailable = true
        },
        Layout = new Layout
        {
            LayoutId = "LAYOUT_SNAPSHOT",
            LayoutName = "Snapshot layout",
            FormFactor = "75%",
            KeyCount = 84
        },
        Switch = new KeyboardSwitch
        {
            SwitchId = "SW_SNAPSHOT",
            BrandId = 1,
            SwitchName = "Current catalog switch",
            SwitchTechnology = "Mechanical",
            MountType = "MX 5-pin",
            PriceUsd = 99m,
            IsAvailable = true
        }
    };

static async Task<BuildRequest> SendSnapshotRequestAsync(
    KeyboardBuild build,
    StubSnapshotCatalog catalog)
{
    var buildRepository = new StubBuildRepository { Build = build };
    var requestRepository = new StubRequestRepository
    {
        Request = new BuildRequest
        {
            RequestId = "REQ_INACTIVE",
            BuildId = "BUILD_OTHER",
            Status = RequestStatus.Cancelled
        }
    };
    var sellerRepository = new StubSellerRepository
    {
        Seller = new SellerProfile
        {
            UserId = 23,
            ShopName = "Snapshot shop",
            Phone = "0900000000",
            Address = "Verification",
            IsVerified = true
        }
    };
    var service = new RequestService(
        buildRepository,
        sellerRepository,
        requestRepository,
        new BuildService(buildRepository, catalog),
        catalog,
        null!,
        new StubRealtimeNotifier());

    return await service.SendRequestAsync(build.BuildId, build.BuyerId, sellerRepository.Seller.UserId, null);
}

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

file sealed class StubBuildRepository : IBuildRepository
{
    public required KeyboardBuild Build { get; init; }

    public Task<IReadOnlyList<KeyboardBuild>> GetByBuyerAsync(
        int buyerId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<KeyboardBuild>>([Build]);

    public Task<KeyboardBuild?> GetByIdAsync(
        string buildId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<KeyboardBuild?>(
            string.Equals(buildId, Build.BuildId, StringComparison.OrdinalIgnoreCase) ? Build : null);

    public Task<KeyboardBuild> SaveAsync(
        KeyboardBuild build,
        CancellationToken cancellationToken = default)
        => Task.FromResult(build);

    public Task SetStatusAsync(
        string buildId,
        BuildStatus status,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ArchiveAsync(string buildId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

file sealed class StubSellerRepository : ISellerRepository
{
    public required SellerProfile Seller { get; init; }

    public Task<SellerProfile?> GetBySellerUserIdAsync(
        int sellerUserId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<SellerProfile?>(sellerUserId == Seller.UserId ? Seller : null);

    public Task<IReadOnlyList<AdminSellerProfileRow>> GetAdminSellerProfilesAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AdminSellerProfileRow>>([]);

    public Task<IReadOnlyList<SellerProfile>> GetVerifiedSellersAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SellerProfile>>([Seller]);

    public Task<int> GetSellerCountAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(1);

    public Task<SellerProfile> SaveAsync(
        SellerProfile sellerProfile,
        CancellationToken cancellationToken = default)
        => Task.FromResult(sellerProfile);

    public Task SetVerifiedAsync(
        int sellerUserId,
        bool isVerified,
        int adminUserId,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

file sealed class StubSnapshotCatalog : IComponentCatalogService
{
    public required KeyboardKit Kit { get; init; }
    public required Layout Layout { get; init; }
    public required KeyboardSwitch Switch { get; init; }
    public int SwitchByIdCalls { get; private set; }

    public Task<IReadOnlyList<Brand>> GetBrandsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Brand>>([]);

    public Task<IReadOnlyList<Layout>> GetLayoutsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Layout>>([Layout]);

    public Task<Layout?> GetLayoutByIdAsync(
        string layoutId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Layout?>(
            string.Equals(layoutId, Layout.LayoutId, StringComparison.OrdinalIgnoreCase) ? Layout : null);

    public Task<IReadOnlyList<KeyboardKit>> GetAvailableKitsAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<KeyboardKit>>([Kit]);

    public Task<KeyboardKit?> GetKitByIdAsync(
        string kitId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<KeyboardKit?>(
            string.Equals(kitId, Kit.KitId, StringComparison.OrdinalIgnoreCase) ? Kit : null);

    public Task<IReadOnlyList<KeyboardSwitch>> GetAvailableSwitchesAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<KeyboardSwitch>>([Switch]);

    public Task<KeyboardSwitch?> GetSwitchByIdAsync(
        string switchId,
        CancellationToken cancellationToken = default)
    {
        SwitchByIdCalls++;
        return Task.FromResult<KeyboardSwitch?>(
            string.Equals(switchId, Switch.SwitchId, StringComparison.OrdinalIgnoreCase) ? Switch : null);
    }

    public Task<IReadOnlyList<KeycapSet>> GetAvailableKeycapSetsAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<KeycapSet>>([]);

    public Task<KeycapSet?> GetKeycapSetByIdAsync(
        string keycapId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<KeycapSet?>(null);

    public Task<IReadOnlyList<Stabilizer>> GetAvailableStabilizersAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Stabilizer>>([]);

    public Task<Stabilizer?> GetStabilizerByIdAsync(
        string stabilizerId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Stabilizer?>(null);

    public Task<IReadOnlyList<Accessory>> GetAvailableAccessoriesAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Accessory>>([]);

    public Task<Accessory?> GetAccessoryByIdAsync(
        string accessoryId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<Accessory?>(null);
}

file sealed class StubRealtimeNotifier : IRealtimeNotifier
{
    public Task RequestCreatedAsync(
        int sellerUserId,
        string requestId,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RequestStatusChangedAsync(
        string requestId,
        RequestStatus status,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
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
