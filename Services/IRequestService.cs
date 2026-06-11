using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Builds;
using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Services;

public interface IRequestService
{
    Task<IReadOnlyList<SellerProfile>> GetAvailableSellersAsync(int buyerId, CancellationToken cancellationToken = default);
    Task<BuildRequest> SendRequestAsync(string buildId, int buyerId, int sellerUserId, string? note, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BuildRequest>> GetBuyerRequestsAsync(int buyerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BuildRequest>> GetSellerRequestsAsync(int sellerUserId, CancellationToken cancellationToken = default);
    Task<BuildRequest> UpdateStatusAsync(string requestId, int sellerUserId, RequestStatus status, CancellationToken cancellationToken = default);
}
