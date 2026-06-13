using System.IO;

namespace Custom_keyboard.Localization;

/// <summary>
/// Best-effort persistence of UI preferences (currently just the language) to a small text file
/// under %LOCALAPPDATA%\CustomKeyboard. All failures fall back to defaults so the UI never crashes
/// because of a preferences read/write.
/// </summary>
internal static class UserPreferencesStore
{
    private static string FilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CustomKeyboard");
            return Path.Combine(dir, "preferences.txt");
        }
    }

    public static AppLanguage LoadLanguage()
    {
        try
        {
            var path = FilePath;
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path).Trim();
                if (Enum.TryParse<AppLanguage>(text, ignoreCase: true, out var language))
                {
                    return language;
                }
            }
        }
        catch
        {
            // Ignore – fall back to the default language.
        }

        return AppLanguage.Vietnamese;
    }

    public static void SaveLanguage(AppLanguage language)
    {
        try
        {
            var path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, language.ToString());
        }
        catch
        {
            // Ignore – persistence is best-effort.
        }
    }
}
