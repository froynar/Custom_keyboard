namespace Custom_keyboard.Services.Stats;

/// <summary>Time grouping for revenue/orders series.</summary>
public enum StatsPeriod
{
    Monthly,
    Quarterly,
    Yearly
}

/// <summary>One point on a revenue/orders time series (label = period, e.g. "2026-05").</summary>
public sealed record TimeBucket(string Label, int Orders, decimal Revenue);

/// <summary>Count of requests in a given status (status kept as the raw enum name).</summary>
public sealed record StatusSlice(string Status, int Count);

/// <summary>Completed-order rollup for one kit.</summary>
public sealed record KitSales(string KitId, string KitName, int Orders, decimal Revenue);

/// <summary>A seller ranked by completed-order revenue (admin top-sellers).</summary>
public sealed record SellerRank(int SellerUserId, string ShopName, int ProductsMade, decimal Revenue);

/// <summary>User count in one role for admin overview.</summary>
public sealed record RoleUserCount(string Role, int Count);

/// <summary>Full analytics payload for a seller's own dashboard.</summary>
public sealed class SellerDashboardStats
{
    public decimal TotalRevenue { get; init; }
    public int ProductsMade { get; init; }
    public int TotalCustomers { get; init; }
    public int InProgressOrders { get; init; }
    public double? AvgCompletionDays { get; init; }
    public IReadOnlyList<TimeBucket> TimeSeries { get; init; } = [];
    public IReadOnlyList<StatusSlice> StatusBreakdown { get; init; } = [];
    public IReadOnlyList<KitSales> TopKits { get; init; } = [];
}

/// <summary>Public, non-financial seller stats shown to buyers when picking a seller.</summary>
public sealed class SellerPublicStats
{
    public int ProductsMade { get; init; }
    public int TotalOrders { get; init; }
    public int Customers { get; init; }
    public bool IsVerified { get; init; }
    public DateTime? VerifiedAt { get; init; }
    public double? AvgCompletionDays { get; init; }
}

/// <summary>System-wide analytics payload for the admin overview.</summary>
public sealed class AdminOverviewStats
{
    public int TotalUsers { get; init; }
    public IReadOnlyList<RoleUserCount> UsersByRole { get; init; } = [];
    public int VerifiedSellers { get; init; }
    public int TotalBuilds { get; init; }
    public int TotalRequests { get; init; }
    public decimal TotalRevenue { get; init; }
    public int CompletedOrders { get; init; }
    public IReadOnlyList<TimeBucket> RevenueSeries { get; init; } = [];
    public IReadOnlyList<StatusSlice> StatusBreakdown { get; init; } = [];
    public IReadOnlyList<SellerRank> TopSellers { get; init; } = [];
}
