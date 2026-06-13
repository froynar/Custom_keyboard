using Custom_keyboard.Services.Stats;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Custom_keyboard.Analytics;

// Centralises all LiveCharts2 series/axis construction so the ViewModels only deal with DTOs.
// Colours mirror the WPF theme palette (Themes/Colors.xaml).
public static class ChartFactory
{
    private static readonly SKColor Accent = new(0x1A, 0xBB, 0x9C);
    private static readonly SKColor Info = new(0x34, 0x98, 0xDB);
    private static readonly SKColor Muted = new(0x73, 0x87, 0x9C);
    private static readonly SKColor Grid = new(0xE6, 0xE9, 0xED);

    /// <summary>Seller chart: revenue columns (left axis) + order count line (right axis).</summary>
    public static ISeries[] SellerRevenueOrders(IReadOnlyList<TimeBucket> buckets) =>
    [
        new ColumnSeries<double>
        {
            Name = "Doanh thu (USD)",
            Values = buckets.Select(b => (double)b.Revenue).ToArray(),
            Fill = new SolidColorPaint(Accent),
            MaxBarWidth = 46,
            ScalesYAt = 0
        },
        new LineSeries<double>
        {
            Name = "So don",
            Values = buckets.Select(b => (double)b.Orders).ToArray(),
            Stroke = new SolidColorPaint(Info, 3),
            Fill = null,
            GeometryStroke = new SolidColorPaint(Info, 3),
            GeometryFill = new SolidColorPaint(SKColors.White),
            GeometrySize = 10,
            LineSmoothness = 0.4,
            ScalesYAt = 1
        }
    ];

    /// <summary>Admin chart: a single revenue line with a soft area fill.</summary>
    public static ISeries[] RevenueLine(IReadOnlyList<TimeBucket> buckets) =>
    [
        new LineSeries<double>
        {
            Name = "Doanh thu (USD)",
            Values = buckets.Select(b => (double)b.Revenue).ToArray(),
            Stroke = new SolidColorPaint(Accent, 3),
            Fill = new SolidColorPaint(new SKColor(0x1A, 0xBB, 0x9C, 38)),
            GeometryStroke = new SolidColorPaint(Accent, 3),
            GeometryFill = new SolidColorPaint(SKColors.White),
            GeometrySize = 9,
            LineSmoothness = 0.4
        }
    ];

    /// <summary>Top sellers ranked by completed-order revenue.</summary>
    public static ISeries[] TopSellersColumns(IReadOnlyList<SellerRank> sellers) =>
    [
        new ColumnSeries<double>
        {
            Name = "Doanh thu (USD)",
            Values = sellers.Select(s => (double)s.Revenue).ToArray(),
            Fill = new SolidColorPaint(Info),
            MaxBarWidth = 42
        }
    ];

    /// <summary>Donut of request status counts; one slice per status, theme-coloured.</summary>
    public static ISeries[] StatusDonut(IReadOnlyList<StatusSlice> slices) =>
        slices.Select(s => (ISeries)new PieSeries<double>
        {
            Name = $"{Humanize(s.Status)} ({s.Count})",
            Values = [(double)s.Count],
            InnerRadius = 55,
            Fill = new SolidColorPaint(StatusColor(s.Status))
        }).ToArray();

    /// <summary>X axis built from period/category labels.</summary>
    public static Axis[] LabelAxis(IEnumerable<string> labels) =>
    [
        new Axis
        {
            Labels = labels.ToArray(),
            LabelsRotation = 0,
            TextSize = 12,
            LabelsPaint = new SolidColorPaint(Muted),
            SeparatorsPaint = new SolidColorPaint(Grid)
        }
    ];

    /// <summary>Dual Y axes for the seller revenue/orders chart.</summary>
    public static Axis[] RevenueOrdersYAxes() =>
    [
        new Axis
        {
            Name = "USD",
            TextSize = 11,
            MinLimit = 0,
            NamePaint = new SolidColorPaint(Muted),
            LabelsPaint = new SolidColorPaint(Muted),
            SeparatorsPaint = new SolidColorPaint(Grid)
        },
        new Axis
        {
            Name = "So don",
            Position = AxisPosition.End,
            TextSize = 11,
            MinLimit = 0,
            NamePaint = new SolidColorPaint(Muted),
            LabelsPaint = new SolidColorPaint(Muted),
            ShowSeparatorLines = false
        }
    ];

    /// <summary>Single Y axis (USD) for the admin revenue line.</summary>
    public static Axis[] RevenueYAxis() =>
    [
        new Axis
        {
            Name = "USD",
            TextSize = 11,
            MinLimit = 0,
            NamePaint = new SolidColorPaint(Muted),
            LabelsPaint = new SolidColorPaint(Muted),
            SeparatorsPaint = new SolidColorPaint(Grid)
        }
    ];

    private static SKColor StatusColor(string status) => status switch
    {
        "Pending" => new SKColor(0xF3, 0x9C, 0x12),
        "Accepted" => new SKColor(0x34, 0x98, 0xDB),
        "In_progress" => new SKColor(0x5B, 0x6B, 0xC0),
        "Completed" => new SKColor(0x26, 0xB9, 0x9A),
        "Cancelled" => new SKColor(0xE7, 0x4C, 0x3C),
        _ => new SKColor(0xBD, 0xC3, 0xC7)
    };

    private static string Humanize(string status) => status.Replace('_', ' ');
}
