using System.Text.Json;

namespace RoManager;

/**
 * Язык интерфейса. Хранится в незашифрованном ui.json рядом с exe,
 * поэтому доступен ещё до входа (мастер авторизации, экран входа).
 * Не содержит пользовательских данных и не требует ключа.
 */
internal static class Loc
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
        catch { /* повреждённый файл — остаётся ru */ }
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

    /** Обновить системные заголовки окон после смены языка. */
    public static void ApplyTitles()
    {
        try { MainFrame.Inst?.ApplyLanguage(); } catch { }
        try { SettingsFrame.Inst?.ApplyLanguage(); } catch { }
        try { BrowserLoginFrame.Inst?.ApplyLanguage(); } catch { }
    }
}
