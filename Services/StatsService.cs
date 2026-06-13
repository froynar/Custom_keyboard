using Custom_keyboard.Localization;
using Custom_keyboard.Repositories;
using Custom_keyboard.Models.Enums;
using Custom_keyboard.Services.Stats;

namespace Custom_keyboard.Services;

// Read-only analytics orchestration with role/active revalidation at the service boundary.
public sealed class StatsService : IStatsService
{
    private readonly IStatsRepository _statsRepository;
    private readonly IUserRepository _userRepository;
    private readonly ISellerRepository _sellerRepository;

    public StatsService(
        IStatsRepository statsRepository,
        IUserRepository userRepository,
        ISellerRepository sellerRepository)
    {
        _statsRepository = statsRepository;
        _userRepository = userRepository;
        _sellerRepository = sellerRepository;
    }

    public async Task<SellerDashboardStats> GetSellerDashboardAsync(int sellerUserId, StatsPeriod period, CancellationToken cancellationToken = default)
    {
        await EnsureActiveRoleAsync(sellerUserId, UserRole.Seller, "Seller", cancellationToken);
        return await _statsRepository.GetSellerDashboardAsync(sellerUserId, period, cancellationToken);
    }

    public async Task<SellerPublicStats> GetSellerPublicAsync(int requesterUserId, int sellerUserId, CancellationToken cancellationToken = default)
    {
        await EnsureActiveUserAsync(requesterUserId, "Requester", cancellationToken);
        await EnsureActiveRoleAsync(sellerUserId, UserRole.Seller, "Seller", cancellationToken);
        var seller = await _sellerRepository.GetBySellerUserIdAsync(sellerUserId, cancellationToken)
            ?? throw new InvalidOperationException(Loc.Instance["Service_SellerNoProfile"]);

        if (!seller.IsVerified)
        {
            throw new InvalidOperationException(Loc.Instance["Service_SellerNotVerified"]);
        }

        return await _statsRepository.GetSellerPublicAsync(sellerUserId, cancellationToken);
    }

    public async Task<AdminOverviewStats> GetAdminOverviewAsync(int adminUserId, StatsPeriod period, CancellationToken cancellationToken = default)
    {
        await EnsureActiveRoleAsync(adminUserId, UserRole.Admin, "Admin", cancellationToken);
        return await _statsRepository.GetAdminOverviewAsync(period, cancellationToken);
    }

    private async Task EnsureActiveUserAsync(int userId, string roleLabel, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException(Loc.Instance.Format("Service_InvalidEntity", roleLabel));

        if (!user.IsActive)
        {
            throw new InvalidOperationException(Loc.Instance.Format("Service_EntityLocked", roleLabel));
        }
    }

    private async Task EnsureActiveRoleAsync(int userId, UserRole role, string roleLabel, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException(Loc.Instance.Format("Service_InvalidEntity", roleLabel));

        if (user.Role != role)
        {
            throw new InvalidOperationException(Loc.Instance.Format("Service_UserNotRole", roleLabel));
        }

        if (!user.IsActive)
        {
            throw new InvalidOperationException(Loc.Instance.Format("Service_EntityLocked", roleLabel));
        }
    }
}
