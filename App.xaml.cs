using System.Windows;
using System.Windows.Threading;

namespace Custom_keyboard
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Surface unhandled exceptions instead of letting the process die silently.
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            base.OnStartup(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ShowError("UI thread", e.Exception);
            e.Handled = true; // keep the window open so the user can keep working
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                ShowError("AppDomain", ex);
            }
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            ShowError("Background task", e.Exception);
            e.SetObserved();
        }

        private static void ShowError(string source, Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                $"Loi ({source})",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
