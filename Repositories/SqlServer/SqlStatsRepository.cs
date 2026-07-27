using System.Data;
using Custom_keyboard.Data.SqlServer;
using Custom_keyboard.Repositories;
using Custom_keyboard.Services.Stats;
using Microsoft.Data.SqlClient;

namespace Custom_keyboard.Repositories.SqlServer;

// Read-only analytics: every figure is derived from build_requests + builds (+ seller_profiles / keyboard_kits).
// No schema changes; status is stored as the raw enum name ("Completed", "In_progress", ...).
public sealed class SqlStatsRepository : IStatsRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SqlStatsRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<SellerDashboardStats> GetSellerDashboardAsync(
        int sellerUserId,
        StatsPeriod period,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var (revenue, productsMade, customers, inProgress, avgDays) =
            await ReadSellerKpisAsync(connection, sellerUserId, cancellationToken);
        var timeSeries = await ReadTimeSeriesAsync(connection, period, sellerUserId, cancellationToken);
        var statusBreakdown = await ReadStatusBreakdownAsync(connection, sellerUserId, cancellationToken);
        var topKits = await ReadTopKitsAsync(connection, sellerUserId, cancellationToken);

        return new SellerDashboardStats
        {
            TotalRevenue = revenue,
            ProductsMade = productsMade,
            TotalCustomers = customers,
            InProgressOrders = inProgress,
            AvgCompletionDays = avgDays,
            TimeSeries = timeSeries,
            StatusBreakdown = statusBreakdown,
            TopKits = topKits
        };
    }

    public async Task<SellerPublicStats> GetSellerPublicAsync(
        int sellerUserId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                SUM(CASE WHEN br.status = 'Completed' THEN 1 ELSE 0 END) AS products_made,
                COUNT(*) AS total_orders,
                COUNT(DISTINCT b.buyer_id) AS customers,
                AVG(CASE WHEN br.status = 'Completed' AND br.accepted_at IS NOT NULL AND br.completed_at IS NOT NULL
                         THEN CAST(DATEDIFF(day, br.accepted_at, br.completed_at) AS float) END) AS avg_days
            FROM build_requests AS br
            INNER JOIN builds AS b ON b.id = br.build_id
            WHERE br.seller_user_id = @seller_id;
            """;
        command.AddParameter("@seller_id", SqlDbType.Int, sellerUserId);

        int productsMade = 0, totalOrders = 0, customers = 0;
        double? avgDays = null;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                productsMade = GetInt(reader, 0);
                totalOrders = GetInt(reader, 1);
                customers = GetInt(reader, 2);
                avgDays = reader.IsDBNull(3) ? null : reader.GetDouble(3);
            }
        }

        var (isVerified, verifiedAt) = await ReadVerificationAsync(connection, sellerUserId, cancellationToken);

        return new SellerPublicStats
        {
            ProductsMade = productsMade,
            TotalOrders = totalOrders,
            Customers = customers,
            IsVerified = isVerified,
            VerifiedAt = verifiedAt,
            AvgCompletionDays = avgDays
        };
    }

    public async Task<AdminOverviewStats> GetAdminOverviewAsync(
        StatsPeriod period,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var revenueSeries = await ReadTimeSeriesAsync(connection, period, sellerUserId: null, cancellationToken);
        var statusBreakdown = await ReadStatusBreakdownAsync(connection, sellerUserId: null, cancellationToken);
        var topSellers = await ReadTopSellersAsync(connection, cancellationToken);
        var (totalUsers, usersByRole, verifiedSellers, totalBuilds, totalRequests) =
            await ReadAdminKpisAsync(connection, cancellationToken);

        var totalRevenue = revenueSeries.Sum(b => b.Revenue);
        var completedOrders = revenueSeries.Sum(b => b.Orders);

        return new AdminOverviewStats
        {
            TotalUsers = totalUsers,
            UsersByRole = usersByRole,
            VerifiedSellers = verifiedSellers,
            TotalBuilds = totalBuilds,
            TotalRequests = totalRequests,
            TotalRevenue = totalRevenue,
            CompletedOrders = completedOrders,
            RevenueSeries = revenueSeries,
            StatusBreakdown = statusBreakdown,
            TopSellers = topSellers
        };
    }

    private static async Task<(decimal Revenue, int ProductsMade, int Customers, int InProgress, double? AvgDays)>
        ReadSellerKpisAsync(SqlConnection connection, int sellerUserId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                COALESCE(SUM(CASE WHEN br.status = 'Completed' THEN b.total_cost_snapshot END), 0) AS revenue,
                SUM(CASE WHEN br.status = 'Completed' THEN 1 ELSE 0 END) AS products_made,
                COUNT(DISTINCT b.buyer_id) AS customers,
                SUM(CASE WHEN br.status IN ('Pending', 'Accepted', 'In_progress') THEN 1 ELSE 0 END) AS in_progress,
                AVG(CASE WHEN br.status = 'Completed' AND br.accepted_at IS NOT NULL AND br.completed_at IS NOT NULL
                         THEN CAST(DATEDIFF(day, br.accepted_at, br.completed_at) AS float) END) AS avg_days
            FROM build_requests AS br
            INNER JOIN builds AS b ON b.id = br.build_id
            WHERE br.seller_user_id = @seller_id;
            """;
        command.AddParameter("@seller_id", SqlDbType.Int, sellerUserId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return (0m, 0, 0, 0, null);
        }

        var revenue = reader.IsDBNull(0) ? 0m : reader.GetDecimal(0);
        var productsMade = GetInt(reader, 1);
        var customers = GetInt(reader, 2);
        var inProgress = GetInt(reader, 3);
        double? avgDays = reader.IsDBNull(4) ? null : reader.GetDouble(4);
        return (revenue, productsMade, customers, inProgress, avgDays);
    }

    // sellerUserId == null -> whole platform (admin); otherwise scoped to one seller.
    private static async Task<IReadOnlyList<TimeBucket>> ReadTimeSeriesAsync(
        SqlConnection connection,
        StatsPeriod period,
        int? sellerUserId,
        CancellationToken cancellationToken)
    {
        var labelExpr = period switch
        {
            StatsPeriod.Yearly => "CAST(YEAR(br.completed_at) AS varchar(4))",
            StatsPeriod.Quarterly => "CONCAT(YEAR(br.completed_at), ' Q', DATEPART(QUARTER, br.completed_at))",
            _ => "CONVERT(char(7), br.completed_at, 126)" // 'YYYY-MM'
        };

        var sellerFilter = sellerUserId is null ? string.Empty : "AND br.seller_user_id = @seller_id";

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {labelExpr} AS label,
                   COUNT(*) AS orders,
                   SUM(b.total_cost_snapshot) AS revenue
            FROM build_requests AS br
            INNER JOIN builds AS b ON b.id = br.build_id
            WHERE br.status = 'Completed' AND br.completed_at IS NOT NULL {sellerFilter}
            GROUP BY {labelExpr}
            ORDER BY MIN(br.completed_at);
            """;
        if (sellerUserId is not null)
        {
            command.AddParameter("@seller_id", SqlDbType.Int, sellerUserId.Value);
        }

        var results = new List<TimeBucket>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var label = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var orders = GetInt(reader, 1);
            var revenue = reader.IsDBNull(2) ? 0m : reader.GetDecimal(2);
            results.Add(new TimeBucket(label, orders, revenue));
        }

        return results;
    }

    private static async Task<IReadOnlyList<StatusSlice>> ReadStatusBreakdownAsync(
        SqlConnection connection,
        int? sellerUserId,
        CancellationToken cancellationToken)
    {
        var sellerFilter = sellerUserId is null ? string.Empty : "WHERE br.seller_user_id = @seller_id";

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT br.status AS status, COUNT(*) AS cnt
            FROM build_requests AS br
            {sellerFilter}
            GROUP BY br.status;
            """;
        if (sellerUserId is not null)
        {
            command.AddParameter("@seller_id", SqlDbType.Int, sellerUserId.Value);
        }

        var results = new List<StatusSlice>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new StatusSlice(reader.GetString(0), GetInt(reader, 1)));
        }

        return results;
    }

    private static async Task<IReadOnlyList<KitSales>> ReadTopKitsAsync(
        SqlConnection connection,
        int sellerUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP 5
                b.kit_id AS kit_id,
                k.kit_name AS kit_name,
                COUNT(*) AS orders,
                SUM(b.total_cost_snapshot) AS revenue
            FROM build_requests AS br
            INNER JOIN builds AS b ON b.id = br.build_id
            INNER JOIN keyboard_kits AS k ON k.id = b.kit_id
            WHERE br.seller_user_id = @seller_id AND br.status = 'Completed'
            GROUP BY b.kit_id, k.kit_name
            ORDER BY COUNT(*) DESC, SUM(b.total_cost_snapshot) DESC;
            """;
        command.AddParameter("@seller_id", SqlDbType.Int, sellerUserId);

        var results = new List<KitSales>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var revenue = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3);
            results.Add(new KitSales(reader.GetString(0), reader.GetString(1), GetInt(reader, 2), revenue));
        }

        return results;
    }

    private static async Task<IReadOnlyList<SellerRank>> ReadTopSellersAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP 5
                br.seller_user_id AS seller_user_id,
                COALESCE(sp.shop_name, CONCAT('Seller #', br.seller_user_id)) AS shop_name,
                COUNT(*) AS products_made,
                SUM(b.total_cost_snapshot) AS revenue
            FROM build_requests AS br
            INNER JOIN builds AS b ON b.id = br.build_id
            LEFT JOIN seller_profiles AS sp ON sp.user_id = br.seller_user_id
            WHERE br.status = 'Completed'
            GROUP BY br.seller_user_id, sp.shop_name
            ORDER BY SUM(b.total_cost_snapshot) DESC;
            """;

        var results = new List<SellerRank>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var revenue = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3);
            results.Add(new SellerRank(GetInt(reader, 0), reader.GetString(1), GetInt(reader, 2), revenue));
        }

        return results;
    }

    private static async Task<(int TotalUsers, IReadOnlyList<RoleUserCount> UsersByRole, int VerifiedSellers, int TotalBuilds, int TotalRequests)>
        ReadAdminKpisAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var summaryCommand = connection.CreateCommand();
        summaryCommand.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM users) AS total_users,
                (SELECT COUNT(*) FROM seller_profiles WHERE is_verified = 1) AS verified_sellers,
                (SELECT COUNT(*) FROM builds) AS total_builds,
                (SELECT COUNT(*) FROM build_requests) AS total_requests;
            """;

        int totalUsers = 0, verifiedSellers = 0, totalBuilds = 0, totalRequests = 0;
        await using (var reader = await summaryCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                totalUsers = GetInt(reader, 0);
                verifiedSellers = GetInt(reader, 1);
                totalBuilds = GetInt(reader, 2);
                totalRequests = GetInt(reader, 3);
            }
        }

        await using var roleCommand = connection.CreateCommand();
        roleCommand.CommandText = """
            SELECT r.role_name, COUNT(u.id) AS user_count
            FROM roles AS r
            LEFT JOIN users AS u ON u.role_id = r.id
            GROUP BY r.role_name
            ORDER BY r.role_name;
            """;

        var usersByRole = new List<RoleUserCount>();
        await using (var reader = await roleCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                usersByRole.Add(new RoleUserCount(reader.GetString(0), GetInt(reader, 1)));
            }
        }

        return (totalUsers, usersByRole, verifiedSellers, totalBuilds, totalRequests);
    }

    private static async Task<(bool IsVerified, DateTime? VerifiedAt)> ReadVerificationAsync(
        SqlConnection connection,
        int sellerUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CAST(COALESCE(is_verified, 0) AS bit) AS is_verified, verified_at
            FROM seller_profiles
            WHERE user_id = @seller_id;
            """;
        command.AddParameter("@seller_id", SqlDbType.Int, sellerUserId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return (false, null);
        }

        var isVerified = !reader.IsDBNull(0) && reader.GetBoolean(0);
        DateTime? verifiedAt = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
        return (isVerified, verifiedAt);
    }

    // SUM(CASE ...) over int can come back typed as int or long depending on the provider; read defensively.
    private static int GetInt(SqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return 0;
        }

        return Convert.ToInt32(reader.GetValue(ordinal));
    }
}
