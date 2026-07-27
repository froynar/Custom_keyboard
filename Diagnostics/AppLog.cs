using System.Data.Common;
using System.IO;
using System.Text;
using Custom_keyboard.Localization;

namespace Custom_keyboard.Diagnostics;

/// <summary>
/// Minimal, dependency-free diagnostics for Phase 7 hardening: appends errors to
/// <c>%LOCALAPPDATA%/CustomKeyboard/log.txt</c> and maps exceptions to short,
/// user-friendly messages. Every method is best-effort and never throws.
/// </summary>
public static class AppLog
{
    private const long MaxLogFileBytes = 2 * 1024 * 1024;
    private static readonly object Gate = new();

    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CustomKeyboard");

    /// <summary>Full path of the rolling log file shown to the user in error dialogs.</summary>
    public static string LogFilePath { get; } = Path.Combine(LogDirectory, "log.txt");

    /// <summary>Previous bounded log generation retained for diagnostics.</summary>
    public static string PreviousLogFilePath { get; } = Path.Combine(LogDirectory, "log.previous.txt");

    /// <summary>Applies the size bound at startup even when no new error is written.</summary>
    public static void Initialize()
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(LogDirectory);
                RotateIfNeeded(incomingBytes: 0);
            }
        }
        catch
        {
            // Diagnostics maintenance must never prevent application startup.
        }
    }

    /// <summary>Appends a timestamped entry with the full exception (stack trace included).</summary>
    public static void Error(string context, Exception ex)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(LogDirectory);
                var entry =
                    $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{context}] {ex}"
                    + Environment.NewLine
                    + Environment.NewLine;
                RotateIfNeeded(Encoding.UTF8.GetByteCount(entry));
                File.AppendAllText(LogFilePath, entry, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging is best-effort; an IO/permission failure must never take the app down.
        }
    }

    private static void RotateIfNeeded(int incomingBytes)
    {
        if (!File.Exists(LogFilePath)
            || new FileInfo(LogFilePath).Length + incomingBytes <= MaxLogFileBytes)
        {
            return;
        }

        File.Move(LogFilePath, PreviousLogFilePath, overwrite: true);
    }

    /// <summary>
    /// Short message safe to show in the UI. Database/infrastructure failures collapse to a
    /// generic connectivity hint; business-rule exceptions keep their (already friendly) message.
    /// </summary>
    public static string ToUserMessage(Exception ex)
    {
        return IsDatabaseError(ex)
            ? Loc.Instance["Error_DatabaseUnavailable"]
            : ex.Message;
    }

    /// <summary>True when the exception (or any inner exception) comes from the data layer.</summary>
    public static bool IsDatabaseError(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is DbException or TimeoutException)
            {
                return true;
            }
        }

        return false;
    }
}
