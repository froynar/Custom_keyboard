using System.Windows.Data;
using System.Windows.Markup;

namespace Custom_keyboard.Localization;

/// <summary>
/// XAML markup extension that binds a property to a localized string, e.g.
/// <c>Text="{loc:Tr Login_Title}"</c>. Returns a one-way binding to <see cref="Loc"/>'s indexer so
/// the text updates automatically when the language changes.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = Loc.Instance,
            Mode = BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }
}
