using System.Diagnostics;
using System.Windows;

namespace Custom_keyboard.Diagnostics;

/// <summary>
/// Last-resort error surface for unhandled/background exceptions. Logs the full
/// exception to <see cref="AppLog.LogFilePath"/>, then shows a concise dialog with
/// an opt-in "open the log" button instead of dumping a full stack trace at the user.
/// </summary>
public static class ErrorReporter
{
    public static void Report(string source, Exception ex)
    {
        AppLog.Error(source, ex);

        var result = MessageBox.Show(
            $"{AppLog.ToUserMessage(ex)}\n\nChi tiet ky thuat da duoc ghi vao file log.\nMo file log de xem chi tiet?",
            $"Da xay ra loi ({source})",
            MessageBoxButton.YesNo,
            MessageBoxImage.Error,
            MessageBoxResult.No);

        if (result == MessageBoxResult.Yes)
        {
            OpenLogFile();
        }
    }

    private static void OpenLogFile()
    {
        try
        {
            Process.Start(new ProcessStartInfo(AppLog.LogFilePath) { UseShellExecute = true });
        }
        catch
        {
            // Opening the log is a convenience; ignore shell failures.
        }
    }
}
