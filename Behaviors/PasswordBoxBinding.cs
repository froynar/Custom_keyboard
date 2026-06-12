using System.Windows;
using System.Windows.Controls;

namespace Custom_keyboard.Behaviors;

// Lets a PasswordBox.Password participate in MVVM bindings (it is not a DependencyProperty).
// Usage: behaviors:PasswordBoxBinding.Attach="True"
//        behaviors:PasswordBoxBinding.BoundPassword="{Binding Password, Mode=TwoWay}"
// The Attach flag guarantees the PasswordChanged handler is wired up; relying on the
// BoundPassword callback alone fails when the initial bound value equals the default ("").
public static class PasswordBoxBinding
{
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword",
            typeof(string),
            typeof(PasswordBoxBinding),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnBoundPasswordChanged));

    public static readonly DependencyProperty AttachProperty =
        DependencyProperty.RegisterAttached(
            "Attach",
            typeof(bool),
            typeof(PasswordBoxBinding),
            new PropertyMetadata(false, OnAttachChanged));

    private static readonly DependencyProperty IsUpdatingProperty =
        DependencyProperty.RegisterAttached(
            "IsUpdating",
            typeof(bool),
            typeof(PasswordBoxBinding),
            new PropertyMetadata(false));

    public static string GetBoundPassword(DependencyObject dependencyObject)
        => (string)dependencyObject.GetValue(BoundPasswordProperty);

    public static void SetBoundPassword(DependencyObject dependencyObject, string value)
        => dependencyObject.SetValue(BoundPasswordProperty, value);

    public static bool GetAttach(DependencyObject dependencyObject)
        => (bool)dependencyObject.GetValue(AttachProperty);

    public static void SetAttach(DependencyObject dependencyObject, bool value)
        => dependencyObject.SetValue(AttachProperty, value);

    private static bool GetIsUpdating(DependencyObject dependencyObject)
        => (bool)dependencyObject.GetValue(IsUpdatingProperty);

    private static void SetIsUpdating(DependencyObject dependencyObject, bool value)
        => dependencyObject.SetValue(IsUpdatingProperty, value);

    private static void OnAttachChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not PasswordBox passwordBox)
        {
            return;
        }

        if ((bool)eventArgs.OldValue)
        {
            passwordBox.PasswordChanged -= OnPasswordChanged;
        }

        if ((bool)eventArgs.NewValue)
        {
            passwordBox.PasswordChanged += OnPasswordChanged;
        }
    }

    private static void OnBoundPasswordChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not PasswordBox passwordBox || GetIsUpdating(passwordBox))
        {
            return;
        }

        var newPassword = eventArgs.NewValue as string ?? string.Empty;
        if (!string.Equals(passwordBox.Password, newPassword, StringComparison.Ordinal))
        {
            passwordBox.Password = newPassword;
        }
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not PasswordBox passwordBox)
        {
            return;
        }

        SetIsUpdating(passwordBox, true);
        SetBoundPassword(passwordBox, passwordBox.Password);
        SetIsUpdating(passwordBox, false);
    }
}
