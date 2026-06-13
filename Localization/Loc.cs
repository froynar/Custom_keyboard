using System.ComponentModel;

namespace Custom_keyboard.Localization;

/// <summary>
/// Central runtime localization source. Exposed as a singleton so both XAML (via the
/// <c>{loc:Tr Key}</c> markup extension / indexer bindings) and code (ViewModels, Services)
/// resolve strings against the same current <see cref="Language"/>.
///
/// Switching <see cref="Language"/> raises <see cref="INotifyPropertyChanged"/> with an empty
/// property name, which refreshes every binding sourced from this instance (including the indexer
/// used by localized text), and fires <see cref="LanguageChanged"/> so ViewModels can re-evaluate
/// their computed string properties.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private AppLanguage _language;

    private Loc()
    {
        _language = UserPreferencesStore.LoadLanguage();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after the language changes. Use a weak subscription from long-lived consumers.</summary>
    public event EventHandler<EventArgs>? LanguageChanged;

    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (_language == value)
            {
                return;
            }

            _language = value;
            UserPreferencesStore.SaveLanguage(value);

            // Empty/null property name => "everything changed", which refreshes indexer bindings too.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            OnPropertyChanged(nameof(Language));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Indexer used by <c>{loc:Tr Key}</c> bindings and by code that wants a raw lookup.</summary>
    public string this[string key] => AppStrings.Get(key, _language);

    /// <summary>Looks up <paramref name="key"/> and formats it with <paramref name="args"/>.</summary>
    public string Format(string key, params object?[] args)
        => string.Format(AppStrings.Get(key, _language), args);

    private void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
