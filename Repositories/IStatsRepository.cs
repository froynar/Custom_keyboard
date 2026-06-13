using Custom_keyboard.Services.Stats;

namespace Custom_keyboard.Repositories;

/// <summary>Read-only aggregate queries that power the analytics dashboards.</summary>
public interface IStatsRepository
{
    Task<SellerDashboardStats> GetSellerDashboardAsync(int sellerUserId, StatsPeriod period, CancellationToken cancellationToken = default);

    Task<SellerPublicStats> GetSellerPublicAsync(int sellerUserId, CancellationToken cancellationToken = default);

    Task<AdminOverviewStats> GetAdminOverviewAsync(StatsPeriod period, CancellationToken cancellationToken = default);
}
