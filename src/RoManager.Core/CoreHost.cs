namespace RoManager.Core;

/**
 * Точка связи ядра с UI. Ядро не знает про WinUI/WinForms:
 * статусы и обновления списка аккаунтов приходят подписчикам событиями.
 */
public static class CoreHost
{
    public static VaultSession Session;

    /** (text, kind) — kind: idle | busy | success | danger. */
    public static Action<string, string> Status;

    /** Запросить у UI перечитывание списка аккаунтов. */
    public static Action AccountsChanged;

    /** Необязательный получатель лога (например, отладочная панель). */
    public static Action<string> LogLine;

    public static void PushStatus(string text, string kind = "idle") =>
        Status?.Invoke(text, kind);

    public static void RequestAccountsRefresh() => AccountsChanged?.Invoke();

    public static void Log(string s)
    {
        try
        {
            LogLine?.Invoke(s);
            var path = Path.Combine(AppContext.BaseDirectory, "rom.log");
            try
            {
                var fi = new FileInfo(path);
                if (fi.Exists && fi.Length > 2 * 1024 * 1024)
                    File.Move(path, path + ".1", true);
            }
            catch { }
            File.AppendAllText(path,
                DateTime.Now.ToString("HH:mm:ss.fff") + " " + s + Environment.NewLine);
        }
        catch { }
    }

    /** Подробный лог только при включённом DevLogMode (после открытия сейва). */
    public static void LogDev(string s)
    {
        try { if (Vault.Settings.DevLogMode) Log(s); } catch { }
    }
}
