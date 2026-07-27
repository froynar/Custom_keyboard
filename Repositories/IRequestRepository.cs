using Custom_keyboard.Models.Builds;
using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Repositories;

public interface IRequestRepository
{
    Task<BuildRequest?> GetByIdAsync(string requestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BuildRequest>> GetByBuyerAsync(int buyerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BuildRequest>> GetBySellerAsync(int sellerUserId, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
    Task<BuildRequest> SaveAsync(BuildRequest request, CancellationToken cancellationToken = default);
    Task<BuildRequest?> TryUpdateStatusAsync(
        BuildRequest request,
        RequestStatus expectedStatus,
        bool requireAcceptableQc,
        CancellationToken cancellationToken = default);

    // Persists the request and its owning build status in one SQL transaction.
    Task<BuildRequest> SaveAndSetBuildStatusAsync(
        BuildRequest request,
        BuildStatus buildStatus,
        CancellationToken cancellationToken = default);
}
