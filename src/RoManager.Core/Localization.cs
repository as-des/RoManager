using System.Text.Json;

namespace RoManager.Core;

/**
 * Core UI language. Stored in an unencrypted ui.json next to the exe, so it is
 * available before the vault is unlocked. The UI layer updates window titles.
 */
public static class Loc
{
    public static string Language { get; private set; } = "ru";
    public static bool IsEnglish => Language == "en";

    public static string T(string ru, string en) => IsEnglish ? en : ru;

    public static string F(string ru, string en, params object[] args) =>
        string.Format(IsEnglish ? en : ru, args);

    static string FilePath => Path.Combine(AppContext.BaseDirectory, "ui.json");

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (doc.RootElement.TryGetProperty("language", out var el))
            {
                var lang = el.GetString();
                if (lang == "en" || lang == "ru") Language = lang;
            }
        }
        catch { /* corrupted file: keep ru */ }
    }

    public static void Set(string lang)
    {
        if (lang != "en" && lang != "ru") return;
        Language = lang;
        try
        {
            File.WriteAllText(FilePath, "{\"language\":\"" + lang + "\"}");
        }
        catch { }
    }
}
