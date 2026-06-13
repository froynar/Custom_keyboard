using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Custom_keyboard.Converters;

/// <summary>
/// Maps a build/request <c>Status</c> enum value to a themed badge brush.
/// Display-only: it never touches the ViewModel, it just looks up a brush by status name.
/// Covers both <c>BuildStatus</c> (Draft/Saved/Requested/Archived) and
/// <c>RequestStatus</c> (Pending/Accepted/In_progress/Completed/Cancelled).
/// </summary>
public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var resourceKey = value?.ToString() switch
        {
            // RequestStatus
            "Pending" => "WarningBrush",
            "Accepted" => "InfoBrush",
            "In_progress" => "InfoBrush",
            "Completed" => "SuccessBrush",
            "Cancelled" => "DangerBrush",
            // BuildStatus
            "Draft" => "DefaultBadgeBrush",
            "Saved" => "InfoBrush",
            "Requested" => "WarningBrush",
            "Archived" => "DefaultBadgeBrush",
            // SellerApplicationStatus
            "Approved" => "SuccessBrush",
            "Rejected" => "DangerBrush",
            _ => "DefaultBadgeBrush"
        };

        return Application.Current?.TryFindResource(resourceKey) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Humanises a status enum name for badge text (e.g. <c>In_progress</c> -&gt; "In progress").
/// </summary>
public sealed class StatusToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString()?.Replace('_', ' ') ?? string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Maps a boolean flag to a badge brush: true -&gt; success (green), false -&gt; muted grey.
/// Used for admin flags such as IsActive / IsVerified / IsAvailable.
/// </summary>
public sealed class BoolToBadgeBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is true ? "SuccessBrush" : "DefaultBadgeBrush";
        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Maps a boolean to one of two labels supplied via ConverterParameter as "OnText|OffText"
/// (e.g. "Active|Banned"). Defaults to "Yes|No" when no parameter is given.
/// </summary>
public sealed class BoolToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var parts = (parameter as string ?? "Yes|No").Split('|');
        var onText = parts.Length > 0 ? parts[0] : "Yes";
        var offText = parts.Length > 1 ? parts[1] : "No";
        return value is true ? onText : offText;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Shows an empty-state overlay when a bound count is zero.
/// Use ConverterParameter="NonEmpty" when the visible condition should be count greater than zero.
/// </summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value switch
        {
            int intValue => intValue,
            System.Collections.ICollection collection => collection.Count,
            _ => 0
        };

        var showWhenNonEmpty = string.Equals(parameter as string, "NonEmpty", StringComparison.OrdinalIgnoreCase);
        var isVisible = showWhenNonEmpty ? count > 0 : count == 0;
        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
