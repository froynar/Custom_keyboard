using Custom_keyboard.Repositories;
using Custom_keyboard.Services.Stats;

namespace Custom_keyboard.Services;

// Thin read-only orchestration over IStatsRepository. The caller passes the seller id it is
// authorised for (a seller VM always passes CurrentUser.UserId), keeping data scoped per role.
public sealed class StatsService : IStatsService
{
    private readonly IStatsRepository _statsRepository;

    public StatsService(IStatsRepository statsRepository)
    {
        _statsRepository = statsRepository;
    }

    public Task<SellerDashboardStats> GetSellerDashboardAsync(int sellerUserId, StatsPeriod period, CancellationToken cancellationToken = default)
        => _statsRepository.GetSellerDashboardAsync(sellerUserId, period, cancellationToken);

    public Task<SellerPublicStats> GetSellerPublicAsync(int sellerUserId, CancellationToken cancellationToken = default)
        => _statsRepository.GetSellerPublicAsync(sellerUserId, cancellationToken);

    public Task<AdminOverviewStats> GetAdminOverviewAsync(StatsPeriod period, CancellationToken cancellationToken = default)
        => _statsRepository.GetAdminOverviewAsync(period, cancellationToken);
}
