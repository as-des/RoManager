namespace RoManager.Core;

/**
 * Bridge between the core and the UI. The core knows nothing about
 * WinUI/WinForms: status updates and account-list refreshes reach
 * subscribers through events.
 */
public static class CoreHost
{
    public static VaultSession Session;

    /** (text, kind) — kind: idle | busy | success | danger. */
    public static Action<string, string> Status;

    /** Ask the UI to reload the account list. */
    public static Action AccountsChanged;

    /** Optional log receiver (for example, a debug panel). */
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

    /** Verbose logging only when DevLogMode is enabled (after the vault is open). */
    public static void LogDev(string s)
    {
        try { if (Vault.Settings.DevLogMode) Log(s); } catch { }
    }
}
