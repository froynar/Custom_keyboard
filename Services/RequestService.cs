using System.Text.Json;
using Custom_keyboard.Diagnostics;
using Custom_keyboard.Localization;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Builds;
using Custom_keyboard.Models.Components;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Realtime;
using Custom_keyboard.Repositories;
using Custom_keyboard.Services.Devices;

namespace Custom_keyboard.Services;

public sealed class RequestService : IRequestService
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly RequestStatus[] ActiveStatuses =
    [
        RequestStatus.Pending,
        RequestStatus.Accepted,
        RequestStatus.In_progress
    ];

    private readonly IBuildRepository _buildRepository;
    private readonly ISellerRepository _sellerRepository;
    private readonly IRequestRepository _requestRepository;
    private readonly IBuildService _buildService;
    private readonly IComponentCatalogService _catalogService;
    private readonly IDeviceService _deviceService;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public RequestService(
        IBuildRepository buildRepository,
        ISellerRepository sellerRepository,
        IRequestRepository requestRepository,
        IBuildService buildService,
        IComponentCatalogService catalogService,
        IDeviceService deviceService,
        IRealtimeNotifier realtimeNotifier)
    {
        _buildRepository = buildRepository;
        _sellerRepository = sellerRepository;
        _requestRepository = requestRepository;
        _buildService = buildService;
        _catalogService = catalogService;
        _deviceService = deviceService;
        _realtimeNotifier = realtimeNotifier;
    }

    public Task<IReadOnlyList<SellerProfile>> GetAvailableSellersAsync(
        int buyerId,
        CancellationToken cancellationToken = default)
    {
        if (buyerId <= 0)
        {
            throw new InvalidOperationException(Loc.Instance["Service_InvalidBuyer"]);
        }

        return _sellerRepository.GetVerifiedSellersAsync(cancellationToken);
    }

    public async Task<BuildRequest> SendRequestAsync(
        string buildId,
        int buyerId,
        int sellerUserId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        if (buyerId <= 0)
        {
            throw new InvalidOperationException(Loc.Instance["Service_InvalidBuyer"]);
        }

        if (sellerUserId <= 0)
        {
            throw new InvalidOperationException(Loc.Instance["Buyer_SelectSellerFirst"]);
        }

        var build = await _buildRepository.GetByIdAsync(buildId, cancellationToken)
            ?? throw new InvalidOperationException(Loc.Instance["Build_NotExist"]);

        if (build.BuyerId != buyerId)
        {
            throw new InvalidOperationException(Loc.Instance["Build_NotOwned"]);
        }

        var validation = await _buildService.ValidateBuildAsync(build, cancellationToken);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                Loc.Instance["Service_BuildNotReady"] + Environment.NewLine + string.Join(Environment.NewLine, validation.Errors));
        }

        await EnsureNoActiveRequestAsync(build, buyerId, cancellationToken);

        var seller = await GetAvailableSellerAsync(sellerUserId, cancellationToken)
            ?? throw new InvalidOperationException(Loc.Instance["Service_SellerNotVerifiedOrInactive"]);

        build.TotalCostSnapshot = validation.TotalCost;
        var payloadJson = await CreateSnapshotJsonAsync(build, seller, validation.TotalCost, cancellationToken);

        var request = new BuildRequest
        {
            BuildId = build.BuildId,
            SellerUserId = sellerUserId,
            RequestPayloadJson = payloadJson,
            Status = RequestStatus.Pending,
            Note = NormalizeNullable(note)
        };

        // Request creation and the owning build status are one unit. A failed write must never
        // leave a Pending request paired with a non-Requested build (or the reverse).
        var saved = await _requestRepository.SaveAndSetBuildStatusAsync(
            request,
            BuildStatus.Requested,
            cancellationToken);
        await PublishSafelyAsync(() =>
            _realtimeNotifier.RequestCreatedAsync(saved.SellerUserId, saved.RequestId, cancellationToken));
        return saved;
    }

    public Task<IReadOnlyList<BuildRequest>> GetBuyerRequestsAsync(
        int buyerId,
        CancellationToken cancellationToken = default)
        => _requestRepository.GetByBuyerAsync(buyerId, cancellationToken);

    public Task<IReadOnlyList<BuildRequest>> GetSellerRequestsAsync(
        int sellerUserId,
        CancellationToken cancellationToken = default)
        => _requestRepository.GetBySellerAsync(sellerUserId, cancellationToken);

    public async Task<BuildRequest> UpdateStatusAsync(
        string requestId,
        int sellerUserId,
        RequestStatus status,
        CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, cancellationToken)
            ?? throw new InvalidOperationException(Loc.Instance["Service_RequestNotExist"]);

        if (request.SellerUserId != sellerUserId)
        {
            throw new InvalidOperationException(Loc.Instance["Service_RequestNotOwned"]);
        }

        if (!CanTransition(request.Status, status))
        {
            throw new InvalidOperationException(Loc.Instance.Format(
                "Service_InvalidTransition",
                Loc.Instance["Status_" + request.Status],
                Loc.Instance["Status_" + status]));
        }

        if (status == RequestStatus.Completed)
        {
            await EnsureQcAllowsCompletionAsync(request.RequestId, cancellationToken);
        }

        var expectedStatus = request.Status;
        request.Status = status;
        if (status == RequestStatus.Accepted && request.AcceptedAt is null)
        {
            request.AcceptedAt = DateTime.UtcNow;
        }

        if (status == RequestStatus.Completed && request.CompletedAt is null)
        {
            request.CompletedAt = DateTime.UtcNow;
        }

        var saved = await _requestRepository.TryUpdateStatusAsync(
            request,
            expectedStatus,
            status == RequestStatus.Completed,
            cancellationToken)
            ?? throw new InvalidOperationException(Loc.Instance["Service_RequestChanged"]);
        await PublishSafelyAsync(() =>
            _realtimeNotifier.RequestStatusChangedAsync(saved.RequestId, saved.Status, cancellationToken));
        return saved;
    }

    private async Task EnsureQcAllowsCompletionAsync(string requestId, CancellationToken cancellationToken)
    {
        var latestSession = await _deviceService.GetLatestSessionByRequestAsync(requestId, cancellationToken);
        if (latestSession is null
            || latestSession.Status is not (TestSessionStatus.Passed or TestSessionStatus.Warning)
            || latestSession.TestedKeys != latestSession.TotalKeys
            || latestSession.FailedKeys > 0
            || (latestSession.Status == TestSessionStatus.Passed && latestSession.WarningKeys > 0)
            || (latestSession.Status == TestSessionStatus.Warning && latestSession.WarningKeys == 0))
        {
            throw new InvalidOperationException(Loc.Instance["Service_QcRequiredBeforeComplete"]);
        }
    }

    // Realtime is a bonus layer: a publish failure must never surface to the caller or
    // undo the committed DB change.
    private static async Task PublishSafelyAsync(Func<Task> publish)
    {
        try
        {
            await publish();
        }
        catch (Exception ex)
        {
            AppLog.Error("RequestService.Realtime", ex);
        }
    }

    private static bool CanTransition(RequestStatus currentStatus, RequestStatus nextStatus)
    {
        if (currentStatus == nextStatus)
        {
            return false;
        }

        return currentStatus switch
        {
            RequestStatus.Pending => nextStatus is RequestStatus.Accepted or RequestStatus.Cancelled,
            RequestStatus.Accepted => nextStatus is RequestStatus.In_progress or RequestStatus.Cancelled,
            RequestStatus.In_progress => nextStatus is RequestStatus.Completed or RequestStatus.Cancelled,
            RequestStatus.Completed or RequestStatus.Cancelled => false,
            _ => false
        };
    }

    private async Task EnsureNoActiveRequestAsync(KeyboardBuild build, int buyerId, CancellationToken cancellationToken)
    {
        var buyerRequests = await _requestRepository.GetByBuyerAsync(buyerId, cancellationToken);
        var hasActive = buyerRequests.Any(request =>
            string.Equals(request.BuildId, build.BuildId, StringComparison.OrdinalIgnoreCase)
            && ActiveStatuses.Contains(request.Status));

        if (hasActive)
        {
            throw new InvalidOperationException(Loc.Instance["Service_BuildHasActiveRequest"]);
        }
    }

    private async Task<SellerProfile?> GetAvailableSellerAsync(int sellerUserId, CancellationToken cancellationToken)
    {
        var sellers = await _sellerRepository.GetVerifiedSellersAsync(cancellationToken);
        return sellers.FirstOrDefault(item => item.UserId == sellerUserId);
    }

    private async Task<string> CreateSnapshotJsonAsync(
        KeyboardBuild build,
        SellerProfile seller,
        decimal totalCost,
        CancellationToken cancellationToken)
    {
        var kit = string.IsNullOrWhiteSpace(build.KitId)
            ? null
            : await _catalogService.GetKitByIdAsync(build.KitId, cancellationToken);

        var items = new List<ItemSnapshot>(build.Items.Count);
        foreach (var item in build.Items)
        {
            items.Add(await CreateItemSnapshotAsync(item, cancellationToken));
        }

        var snapshot = new BuildRequestSnapshot(
            Build: new BuildSnapshot(
                build.BuildId,
                build.Name,
                build.Notes,
                build.NoiseRequirement.ToString(),
                build.Status.ToString(),
                totalCost,
                build.CreatedAt,
                build.UpdatedAt),
            Seller: new SellerSnapshot(seller.UserId, seller.ShopName, seller.Phone, seller.Address),
            Kit: kit is null
                ? null
                : new KitSnapshot(
                    kit.KitId,
                    kit.KitName,
                    kit.BrandId,
                    kit.LayoutId,
                    kit.PcbTechnology,
                    kit.SwitchMount,
                    kit.RequiredSwitchQuantity,
                    kit.IncludedParts,
                    kit.PriceUsd),
            Items: items,
            Mods: build.Mods
                .Select(mod => new ModSnapshot(mod.ModType, mod.TargetComponent, mod.Notes))
                .ToList());

        return JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);
    }

    private async Task<ItemSnapshot> CreateItemSnapshotAsync(BuildItem item, CancellationToken cancellationToken)
    {
        if (HasPersistedCatalogProjection(item))
        {
            return new ItemSnapshot(
                NormalizeSnapshotItemType(item.ComponentType),
                item.ComponentId,
                item.ComponentName,
                item.Quantity,
                item.UnitPriceSnapshot,
                item.LineTotalSnapshot,
                item.Notes);
        }

        string productType;
        string productId;
        string productName;

        if (item.SwitchId is not null)
        {
            var product = await _catalogService.GetSwitchByIdAsync(item.SwitchId, cancellationToken);
            (productType, productId, productName) =
                ("Switch", item.SwitchId, product?.SwitchName ?? item.SwitchId);
        }
        else if (item.KeycapId is not null)
        {
            var product = await _catalogService.GetKeycapSetByIdAsync(item.KeycapId, cancellationToken);
            (productType, productId, productName) =
                ("Keycap", item.KeycapId, product?.KeycapName ?? item.KeycapId);
        }
        else if (item.StabilizerId is not null)
        {
            var product = await _catalogService.GetStabilizerByIdAsync(item.StabilizerId, cancellationToken);
            (productType, productId, productName) =
                ("Stabilizer", item.StabilizerId, product?.StabilizerName ?? item.StabilizerId);
        }
        else
        {
            var product = await _catalogService.GetAccessoryByIdAsync(item.AccessoryId!, cancellationToken);
            (productType, productId, productName) =
                ("Accessory", item.AccessoryId!, product?.AccessoryName ?? item.AccessoryId!);
        }

        return new ItemSnapshot(
            productType,
            productId,
            productName,
            item.Quantity,
            item.UnitPriceSnapshot,
            CalculateLineTotalSnapshot(item.Quantity, item.UnitPriceSnapshot),
            item.Notes);
    }

    private static bool HasPersistedCatalogProjection(BuildItem item)
        => item.BuildItemId > 0
           && !string.IsNullOrWhiteSpace(item.ComponentType)
           && !string.IsNullOrWhiteSpace(item.ComponentId)
           && !string.IsNullOrWhiteSpace(item.ComponentName);

    private static string NormalizeSnapshotItemType(string componentType)
        => string.Equals(componentType.Trim(), "KeycapSet", StringComparison.OrdinalIgnoreCase)
            ? "Keycap"
            : componentType.Trim();

    private static decimal CalculateLineTotalSnapshot(int quantity, decimal unitPriceSnapshot)
        => Math.Round(quantity * unitPriceSnapshot, 2, MidpointRounding.AwayFromZero);

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record BuildRequestSnapshot(
        BuildSnapshot Build,
        SellerSnapshot Seller,
        KitSnapshot? Kit,
        IReadOnlyList<ItemSnapshot> Items,
        IReadOnlyList<ModSnapshot> Mods);

    private sealed record BuildSnapshot(
        string BuildId,
        string Name,
        string? Notes,
        string NoiseRequirement,
        string Status,
        decimal TotalCostSnapshot,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    private sealed record SellerSnapshot(int SellerUserId, string ShopName, string Phone, string Address);

    private sealed record KitSnapshot(
        string KitId,
        string KitName,
        int BrandId,
        string LayoutId,
        string PcbTechnology,
        string SwitchMount,
        int RequiredSwitchQuantity,
        string? IncludedParts,
        decimal PriceUsd);

    private sealed record ItemSnapshot(
        string ProductType,
        string ProductId,
        string ProductName,
        int Quantity,
        decimal UnitPriceSnapshot,
        decimal LineTotal,
        string? Notes);

    private sealed record ModSnapshot(string ModType, string TargetComponent, string? Notes);
}
