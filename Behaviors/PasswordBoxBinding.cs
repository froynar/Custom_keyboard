using System.Windows;
using System.Windows.Controls;

namespace Custom_keyboard.Behaviors;

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

    private static readonly DependencyProperty IsUpdatingProperty =
        DependencyProperty.RegisterAttached(
            "IsUpdating",
            typeof(bool),
            typeof(PasswordBoxBinding),
            new PropertyMetadata(false));

    public static string GetBoundPassword(DependencyObject dependencyObject)
    {
        return (string)dependencyObject.GetValue(BoundPasswordProperty);
    }

    public static void SetBoundPassword(DependencyObject dependencyObject, string value)
    {
        dependencyObject.SetValue(BoundPasswordProperty, value);
    }

    private static bool GetIsUpdating(DependencyObject dependencyObject)
    {
        return (bool)dependencyObject.GetValue(IsUpdatingProperty);
    }

    private static void SetIsUpdating(DependencyObject dependencyObject, bool value)
    {
        dependencyObject.SetValue(IsUpdatingProperty, value);
    }

    private static void OnBoundPasswordChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not PasswordBox passwordBox)
        {
            return;
        }

        passwordBox.PasswordChanged -= OnPasswordChanged;

        if (!GetIsUpdating(passwordBox))
        {
            var newPassword = eventArgs.NewValue as string ?? string.Empty;
            if (!string.Equals(passwordBox.Password, newPassword, StringComparison.Ordinal))
            {
                passwordBox.Password = newPassword;
            }
        }

        passwordBox.PasswordChanged += OnPasswordChanged;
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
