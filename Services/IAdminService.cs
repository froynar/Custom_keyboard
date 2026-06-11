using Custom_keyboard.Models.Admin;
using Custom_keyboard.Models.Accounts;
using Custom_keyboard.Models.Components;
using Custom_keyboard.Models.Enums;

namespace Custom_keyboard.Services;

public interface IAdminService
{
    Task<AdminDashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminSellerProfileRow>> GetSellerProfilesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Brand>> GetBrandsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Layout>> GetLayoutsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminComponentRecord>> GetComponentsAsync(AdminComponentType componentType, CancellationToken cancellationToken = default);
    Task SetUserActiveAsync(int userId, bool isActive, int adminUserId, CancellationToken cancellationToken = default);
    Task SetUserRoleAsync(int userId, UserRole role, int adminUserId, CancellationToken cancellationToken = default);
    Task<SellerProfile> SaveSellerProfileAsync(SellerProfile sellerProfile, int adminUserId, CancellationToken cancellationToken = default);
    Task<SellerProfile> SetSellerVerifiedAsync(int sellerUserId, bool isVerified, int adminUserId, CancellationToken cancellationToken = default);
    Task<Brand> SaveBrandAsync(Brand brand, int adminUserId, CancellationToken cancellationToken = default);
    Task<Layout> SaveLayoutAsync(Layout layout, int adminUserId, CancellationToken cancellationToken = default);
    Task<AdminComponentRecord> SaveComponentAsync(AdminComponentRecord component, int adminUserId, CancellationToken cancellationToken = default);
    Task SetComponentAvailabilityAsync(AdminComponentType componentType, string componentId, bool isAvailable, int adminUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogEntry>> GetAuditLogsAsync(CancellationToken cancellationToken = default);
}
