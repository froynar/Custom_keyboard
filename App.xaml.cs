using System.Windows;
using System.Windows.Threading;
using Custom_keyboard.Diagnostics;

namespace Custom_keyboard
{
    public partial class App : Application
    {
        private bool _isShowingError;

        protected override void OnStartup(StartupEventArgs e)
        {
            AppLog.Initialize();

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

        private void ShowError(string source, Exception ex)
        {
            // Guard against a layout-triggered exception that recurs every render pass
            // stacking dozens of dialogs on top of each other.
            if (_isShowingError)
            {
                return;
            }

            try
            {
                _isShowingError = true;
                ErrorReporter.Report(source, ex);
            }
            finally
            {
                _isShowingError = false;
            }
        }
    }
}
