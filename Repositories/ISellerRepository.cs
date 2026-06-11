using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Admin;

namespace Custom_keyboard.Repositories;

public interface ISellerRepository
{
    Task<SellerProfile?> GetBySellerUserIdAsync(int sellerUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminSellerProfileRow>> GetAdminSellerProfilesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SellerProfile>> GetVerifiedSellersAsync(CancellationToken cancellationToken = default);
    Task<int> GetSellerCountAsync(CancellationToken cancellationToken = default);
    Task<SellerProfile> SaveAsync(SellerProfile sellerProfile, CancellationToken cancellationToken = default);
    Task SetVerifiedAsync(int sellerUserId, bool isVerified, int adminUserId, CancellationToken cancellationToken = default);
}
