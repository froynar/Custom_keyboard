using System.Globalization;
using System.Windows.Data;
using Custom_keyboard.Localization;

namespace Custom_keyboard.Converters;

/// <summary>
/// Maps a status enum value to a localized label. Used as a multi-binding converter together with
/// <c>Loc.Instance.Language</c> so the badge text refreshes when the language changes (the bound
/// item's status itself doesn't change, so a plain converter would never re-run).
/// Key convention: <c>Status_&lt;EnumValue&gt;</c> e.g. <c>Status_In_progress</c>.
/// </summary>
public sealed class LocalizedStatusConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = values is { Length: > 0 } ? values[0]?.ToString() : null;
        return string.IsNullOrEmpty(status) ? string.Empty : Loc.Instance["Status_" + status];
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Maps a boolean to one of two localized labels. Multi-binding converter (value + current language)
/// so badges refresh live. ConverterParameter is "TrueKey|FalseKey" (localization keys).
/// </summary>
public sealed class LocalizedBoolLabelConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var isTrue = values is { Length: > 0 } && values[0] is true;
        var keys = (parameter as string ?? "|").Split('|');
        var key = isTrue ? keys[0] : (keys.Length > 1 ? keys[1] : keys[0]);
        return Loc.Instance[key];
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Two-way converter for binding a group of RadioButtons to an enum-valued property. The
/// ConverterParameter is the enum member name; IsChecked is true when the bound value equals it,
/// and checking a button writes that enum member back.
/// </summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString();

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true && parameter is not null
            ? Enum.Parse(targetType, parameter.ToString()!)
            : Binding.DoNothing;
}
