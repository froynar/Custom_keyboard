using System.Text.Json;
using Custom_keyboard.Diagnostics;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Builds;
using Custom_keyboard.Models.Components;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Realtime;
using Custom_keyboard.Repositories;

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
    private readonly IRealtimeNotifier _realtimeNotifier;

    public RequestService(
        IBuildRepository buildRepository,
        ISellerRepository sellerRepository,
        IRequestRepository requestRepository,
        IBuildService buildService,
        IComponentCatalogService catalogService,
        IRealtimeNotifier realtimeNotifier)
    {
        _buildRepository = buildRepository;
        _sellerRepository = sellerRepository;
        _requestRepository = requestRepository;
        _buildService = buildService;
        _catalogService = catalogService;
        _realtimeNotifier = realtimeNotifier;
    }

    public Task<IReadOnlyList<SellerProfile>> GetAvailableSellersAsync(
        int buyerId,
        CancellationToken cancellationToken = default)
    {
        if (buyerId <= 0)
        {
            throw new InvalidOperationException("Buyer khong hop le.");
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
            throw new InvalidOperationException("Buyer khong hop le.");
        }

        if (sellerUserId <= 0)
        {
            throw new InvalidOperationException("Chon seller truoc khi gui request.");
        }

        var build = await _buildRepository.GetByIdAsync(buildId, cancellationToken)
            ?? throw new InvalidOperationException("Build khong ton tai.");

        if (build.BuyerId != buyerId)
        {
            throw new InvalidOperationException("Build khong thuoc buyer hien tai.");
        }

        var validation = await _buildService.ValidateBuildAsync(build, cancellationToken);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                "Build chua du cau hinh de gui request." + Environment.NewLine + string.Join(Environment.NewLine, validation.Errors));
        }

        await EnsureNoActiveRequestAsync(build, buyerId, cancellationToken);

        var seller = await GetAvailableSellerAsync(sellerUserId, cancellationToken)
            ?? throw new InvalidOperationException("Seller chua verified hoac bi inactive.");

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

        // DB is the source of truth: save first, then publish realtime as a best-effort bonus.
        var saved = await _requestRepository.SaveAsync(request, cancellationToken);
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
            ?? throw new InvalidOperationException("Request khong ton tai.");

        if (request.SellerUserId != sellerUserId)
        {
            throw new InvalidOperationException("Request khong thuoc seller hien tai.");
        }

        if (!CanTransition(request.Status, status))
        {
            throw new InvalidOperationException($"Khong the chuyen request tu {request.Status} sang {status}.");
        }

        request.Status = status;
        if (status == RequestStatus.Accepted && request.AcceptedAt is null)
        {
            request.AcceptedAt = DateTime.UtcNow;
        }

        if (status == RequestStatus.Completed && request.CompletedAt is null)
        {
            request.CompletedAt = DateTime.UtcNow;
        }

        var saved = await _requestRepository.SaveAsync(request, cancellationToken);
        await PublishSafelyAsync(() =>
            _realtimeNotifier.RequestStatusChangedAsync(saved.RequestId, saved.Status, cancellationToken));
        return saved;
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
            throw new InvalidOperationException("Build nay dang co request active; khong the gui them.");
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
        string productType;
        string productId;
        string productName;
        decimal unitPrice;

        if (item.SwitchId is not null)
        {
            var product = await _catalogService.GetSwitchByIdAsync(item.SwitchId, cancellationToken);
            (productType, productId, productName, unitPrice) =
                ("Switch", item.SwitchId, product?.SwitchName ?? item.SwitchId, product?.PriceUsd ?? item.UnitPriceSnapshot);
        }
        else if (item.KeycapId is not null)
        {
            var product = await _catalogService.GetKeycapSetByIdAsync(item.KeycapId, cancellationToken);
            (productType, productId, productName, unitPrice) =
                ("Keycap", item.KeycapId, product?.KeycapName ?? item.KeycapId, product?.PriceUsd ?? item.UnitPriceSnapshot);
        }
        else if (item.StabilizerId is not null)
        {
            var product = await _catalogService.GetStabilizerByIdAsync(item.StabilizerId, cancellationToken);
            (productType, productId, productName, unitPrice) =
                ("Stabilizer", item.StabilizerId, product?.StabilizerName ?? item.StabilizerId, product?.PriceUsd ?? item.UnitPriceSnapshot);
        }
        else
        {
            var product = await _catalogService.GetAccessoryByIdAsync(item.AccessoryId!, cancellationToken);
            (productType, productId, productName, unitPrice) =
                ("Accessory", item.AccessoryId!, product?.AccessoryName ?? item.AccessoryId!, product?.PriceUsd ?? item.UnitPriceSnapshot);
        }

        return new ItemSnapshot(
            productType,
            productId,
            productName,
            item.Quantity,
            unitPrice,
            Math.Round(item.Quantity * unitPrice, 2, MidpointRounding.AwayFromZero),
            item.Notes);
    }

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
