using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Custom_keyboard.Localization;

namespace Custom_keyboard.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    protected ViewModelBase()
    {
        // Weak subscription: when the language changes, re-evaluate every bound string property.
        // WeakEventManager avoids keeping replaced ViewModels alive via the long-lived Loc singleton.
        WeakEventManager<Loc, EventArgs>.AddHandler(Loc.Instance, nameof(Loc.LanguageChanged), OnLanguageChanged);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Look up a localized string by key.</summary>
    protected static string Tr(string key) => Loc.Instance[key];

    /// <summary>Look up a localized format string by key and fill in <paramref name="args"/>.</summary>
    protected static string TrFormat(string key, params object?[] args) => Loc.Instance.Format(key, args);

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Empty property name => refresh all bindings, so computed Tr-based labels pick up the new language.
        OnPropertyChanged(string.Empty);
        OnLanguageChangedCore();
    }

    /// <summary>
    /// Hook for subclasses to refresh artifacts that bindings alone can't (e.g. chart series whose
    /// localized labels are baked into a cached object instance). Called after the blanket
    /// PropertyChanged on every language change.
    /// </summary>
    protected virtual void OnLanguageChangedCore()
    {
    }
}
