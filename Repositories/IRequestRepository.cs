using Custom_keyboard.Models.Builds;

namespace Custom_keyboard.Repositories;

public interface IRequestRepository
{
    Task<BuildRequest?> GetByIdAsync(string requestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BuildRequest>> GetByBuyerAsync(int buyerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BuildRequest>> GetBySellerAsync(int sellerUserId, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
    Task<BuildRequest> SaveAsync(BuildRequest request, CancellationToken cancellationToken = default);
}
