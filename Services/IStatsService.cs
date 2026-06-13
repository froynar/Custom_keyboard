using Custom_keyboard.Services.Stats;

namespace Custom_keyboard.Services;

public interface IStatsService
{
    Task<SellerDashboardStats> GetSellerDashboardAsync(int sellerUserId, StatsPeriod period, CancellationToken cancellationToken = default);

    Task<SellerPublicStats> GetSellerPublicAsync(int sellerUserId, CancellationToken cancellationToken = default);

    Task<AdminOverviewStats> GetAdminOverviewAsync(StatsPeriod period, CancellationToken cancellationToken = default);
}
