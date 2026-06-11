using System.Text.Json;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Builds;
using Custom_keyboard.Models.Components;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Repositories;

namespace Custom_keyboard.Services;

public sealed class RequestService : IRequestService
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IBuildRepository _buildRepository;
    private readonly ISellerRepository _sellerRepository;
    private readonly IRequestRepository _requestRepository;
    private readonly IBuildService _buildService;
    private readonly IComponentCatalogService _catalogService;

    public RequestService(
        IBuildRepository buildRepository,
        ISellerRepository sellerRepository,
        IRequestRepository requestRepository,
        IBuildService buildService,
        IComponentCatalogService catalogService)
    {
        _buildRepository = buildRepository;
        _sellerRepository = sellerRepository;
        _requestRepository = requestRepository;
        _buildService = buildService;
        _catalogService = catalogService;
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

        if (build.UserId != buyerId)
        {
            throw new InvalidOperationException("Build khong thuoc buyer hien tai.");
        }

        var validation = await _buildService.ValidateBuildAsync(build, cancellationToken);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                "Build chua du cau hinh de gui request." + Environment.NewLine + string.Join(Environment.NewLine, validation.Messages));
        }

        var availableSeller = await GetAvailableSellerAsync(sellerUserId, cancellationToken)
            ?? throw new InvalidOperationException("Seller chua verified hoac bi inactive.");

        var payloadJson = await CreateSnapshotJsonAsync(build, availableSeller, cancellationToken);
        var request = new BuildRequest
        {
            BuildId = build.BuildId,
            BuyerId = buyerId,
            SellerUserId = sellerUserId,
            RequestPayloadJson = payloadJson,
            Status = RequestStatus.Pending,
            Note = NormalizeNullable(note)
        };

        return await _requestRepository.SaveAsync(request, cancellationToken);
    }

    public Task<IReadOnlyList<BuildRequest>> GetBuyerRequestsAsync(
        int buyerId,
        CancellationToken cancellationToken = default)
    {
        return _requestRepository.GetByBuyerAsync(buyerId, cancellationToken);
    }

    public Task<IReadOnlyList<BuildRequest>> GetSellerRequestsAsync(
        int sellerUserId,
        CancellationToken cancellationToken = default)
    {
        return _requestRepository.GetBySellerAsync(sellerUserId, cancellationToken);
    }

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

        return await _requestRepository.SaveAsync(request, cancellationToken);
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

    private async Task<SellerProfile?> GetAvailableSellerAsync(
        int sellerUserId,
        CancellationToken cancellationToken)
    {
        var sellers = await _sellerRepository.GetVerifiedSellersAsync(cancellationToken);
        return sellers.FirstOrDefault(item => item.UserId == sellerUserId);
    }

    private async Task<string> CreateSnapshotJsonAsync(
        KeyboardBuild build,
        SellerProfile seller,
        CancellationToken cancellationToken)
    {
        var layout = (await _catalogService.GetLayoutsAsync(cancellationToken))
            .FirstOrDefault(item => Same(item.LayoutId, build.LayoutId));
        var selectedCase = (await _catalogService.GetCasesForLayoutAsync(build.LayoutId, cancellationToken))
            .FirstOrDefault(item => Same(item.CaseId, build.CaseId));
        var selectedPcb = (await _catalogService.GetPcbsForLayoutAsync(build.LayoutId, cancellationToken))
            .FirstOrDefault(item => Same(item.PcbId, build.PcbId));
        var selectedPlate = (await _catalogService.GetPlatesForLayoutAsync(build.LayoutId, cancellationToken))
            .FirstOrDefault(item => Same(item.PlateId, build.PlateId));
        var selectedSwitch = (await _catalogService.GetAvailableSwitchesAsync(cancellationToken))
            .FirstOrDefault(item => Same(item.SwitchId, build.SwitchId));
        var selectedKeycap = (await _catalogService.GetAvailableKeycapSetsAsync(cancellationToken))
            .FirstOrDefault(item => Same(item.KeycapId, build.KeycapId));
        var selectedStabilizer = (await _catalogService.GetAvailableStabilizersAsync(cancellationToken))
            .FirstOrDefault(item => Same(item.StabilizerId, build.StabilizerId));

        var snapshot = new BuildRequestSnapshot(
            Build: new BuildSnapshot(
                build.BuildId,
                build.Name,
                build.Notes,
                build.Status.ToString(),
                build.TotalCostSnapshot,
                build.CreatedAt,
                build.UpdatedAt),
            Seller: new SellerSnapshot(seller.UserId, seller.ShopName, seller.Phone, seller.Address),
            Layout: layout is null
                ? null
                : new LayoutSnapshot(layout.LayoutId, layout.LayoutName, layout.FormFactor, layout.StandardKeyCount),
            Case: selectedCase is null
                ? null
                : new CaseSnapshot(
                    selectedCase.CaseId,
                    selectedCase.BrandId,
                    selectedCase.Material,
                    selectedCase.MountType,
                    selectedCase.Color,
                    selectedCase.WeightG,
                    selectedCase.PriceUsd),
            Pcb: selectedPcb is null
                ? null
                : new PcbSnapshot(
                    selectedPcb.PcbId,
                    selectedPcb.BrandId,
                    selectedPcb.PcbTechnology,
                    selectedPcb.MountType,
                    selectedPcb.SwitchMount,
                    selectedPcb.Hotswap,
                    selectedPcb.Wireless,
                    selectedPcb.Rgb,
                    selectedPcb.PriceUsd),
            Plate: selectedPlate is null
                ? null
                : new PlateSnapshot(
                    selectedPlate.PlateId,
                    selectedPlate.BrandId,
                    selectedPlate.Material,
                    selectedPlate.MountType,
                    selectedPlate.FlexCut,
                    selectedPlate.PriceUsd),
            Switch: selectedSwitch is null
                ? null
                : new SwitchSnapshot(
                    selectedSwitch.SwitchId,
                    selectedSwitch.BrandId,
                    selectedSwitch.SwitchTechnology,
                    selectedSwitch.SwitchType,
                    selectedSwitch.MountType,
                    selectedSwitch.ActuationForceG,
                    selectedSwitch.SoundProfile,
                    selectedSwitch.PriceUsd),
            Keycap: selectedKeycap is null
                ? null
                : new KeycapSnapshot(
                    selectedKeycap.KeycapId,
                    selectedKeycap.BrandId,
                    selectedKeycap.Profile,
                    selectedKeycap.Material,
                    selectedKeycap.ColorPrimary,
                    selectedKeycap.LegendType,
                    selectedKeycap.PriceUsd),
            Stabilizer: selectedStabilizer is null
                ? null
                : new StabilizerSnapshot(
                    selectedStabilizer.StabilizerId,
                    selectedStabilizer.BrandId,
                    selectedStabilizer.StabilizerType,
                    selectedStabilizer.SizesIncluded,
                    selectedStabilizer.PriceUsd),
            Mods: build.Mods.Select(mod => new ModSnapshot(
                mod.ModType,
                mod.TargetComponent,
                mod.LubeType,
                mod.IsFilmed,
                mod.SpringWeightG,
                mod.Notes)).ToList());

        return JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool Same(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed record BuildRequestSnapshot(
        BuildSnapshot Build,
        SellerSnapshot Seller,
        LayoutSnapshot? Layout,
        CaseSnapshot? Case,
        PcbSnapshot? Pcb,
        PlateSnapshot? Plate,
        SwitchSnapshot? Switch,
        KeycapSnapshot? Keycap,
        StabilizerSnapshot? Stabilizer,
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

    private sealed record LayoutSnapshot(string LayoutId, string LayoutName, string FormFactor, int StandardKeyCount);

    private sealed record CaseSnapshot(
        string CaseId,
        int BrandId,
        string Material,
        string MountType,
        string Color,
        int WeightG,
        decimal PriceUsd);

    private sealed record PcbSnapshot(
        string PcbId,
        int BrandId,
        string PcbTechnology,
        string MountType,
        string SwitchMount,
        bool Hotswap,
        bool Wireless,
        bool Rgb,
        decimal PriceUsd);

    private sealed record PlateSnapshot(
        string PlateId,
        int BrandId,
        string Material,
        string MountType,
        string FlexCut,
        decimal PriceUsd);

    private sealed record SwitchSnapshot(
        string SwitchId,
        int BrandId,
        string SwitchTechnology,
        string SwitchType,
        string MountType,
        int ActuationForceG,
        string SoundProfile,
        decimal PriceUsd);

    private sealed record KeycapSnapshot(
        string KeycapId,
        int BrandId,
        string Profile,
        string Material,
        string ColorPrimary,
        string LegendType,
        decimal PriceUsd);

    private sealed record StabilizerSnapshot(
        string StabilizerId,
        int BrandId,
        string StabilizerType,
        string SizesIncluded,
        decimal PriceUsd);

    private sealed record ModSnapshot(
        string ModType,
        string TargetComponent,
        string? LubeType,
        bool IsFilmed,
        int? SpringWeightG,
        string? Notes);
}
