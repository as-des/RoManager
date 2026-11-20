using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RoManager.Core;

/**
 * «Запомнить меня»: сохранённый ключевой файл и код авторизации
 * (фраза + PIN), чтобы следующий запуск открыл хранилище сам.
 *
 * Секреты шифруются DPAPI (CryptProtectData) для текущего пользователя
 * Windows: файл нельзя перенести на другую машину или открыть другим
 * аккаунтом Windows. Срок хранения задаётся в настройках; после
 * истечения сессия стирается и приложение снова просит вход.
 *
 * Конфигурация (включено/срок) хранится в том же файле manager-session.json
 * и доступна до разблокировки хранилища.
 */
public static class SessionStore
{
    public const int DefaultDays = 30;
    public const int MinDays = 1;
    public const int MaxDays = 365;

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "manager-session.json");

    public sealed class Data
    {
        public int Version = 1;
        public bool Enabled = true;
        public int Days = DefaultDays;
        public string SavedAt = "";
        public string ExpiresAt = "";
        public string KeyFileName = "";
        /** Секреты ниже хранятся в base64(DPAPI), а не открытым текстом. */
        public string KeyJson = "";
        public string Phrase = "";
        public string Pin = "";
    }

    static readonly object Lock = new();
    static Data _data;

    public static Data Current
    {
        get { lock (Lock) return _data ??= LoadFile(); }
    }

    public static bool HasCredentials
    {
        get
        {
            var d = Current;
            return d.Enabled && !string.IsNullOrEmpty(d.KeyJson) && !IsExpired(d);
        }
    }

    public static bool IsExpired(Data d) =>
        !string.IsNullOrEmpty(d.ExpiresAt)
        && DateTime.TryParse(d.ExpiresAt, null,
               System.Globalization.DateTimeStyles.RoundtripKind, out var expires)
        && expires < DateTime.UtcNow;

    static Data LoadFile()
    {
        try
        {
            if (!File.Exists(FilePath)) return new Data();
            var d = JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath), Jsons.Opt);
            if (d == null) return new Data();
            d.Days = Math.Clamp(d.Days <= 0 ? DefaultDays : d.Days, MinDays, MaxDays);
            return d;
        }
        catch (Exception ex)
        {
            CoreHost.Log("session load fail: " + ex.Message);
            return new Data();
        }
    }

    static void Write(Data d)
    {
        try
        {
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, Jsons.S(d));
            File.Move(tmp, FilePath, true);
        }
        catch (Exception ex)
        {
            CoreHost.Log("session save fail: " + ex.Message);
        }
    }

    /** Сохранить настройки «запоминать сессию» и срок хранения. */
    public static void Configure(bool enabled, int days)
    {
        lock (Lock)
        {
            var d = _data ??= LoadFile();
            d.Enabled = enabled;
            d.Days = Math.Clamp(days, MinDays, MaxDays);

            if (!enabled)
                ClearSecrets(d);
            else if (!string.IsNullOrEmpty(d.KeyJson))
            {
                // Срок могли уменьшить — пересчитываем дату истечения от сохранения.
                if (DateTime.TryParse(d.SavedAt, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var saved))
                    d.ExpiresAt = saved.AddDays(d.Days).ToString("o");
            }

            Write(d);
        }
    }

    /** Сохранить текущую сессию после успешного входа (если включено). */
    public static void SaveSession(string keyJson, string phrase, string pin, string keyFileName = "")
    {
        if (string.IsNullOrWhiteSpace(keyJson) ||
            string.IsNullOrEmpty(phrase) || string.IsNullOrEmpty(pin)) return;

        lock (Lock)
        {
            var d = _data ??= LoadFile();
            if (!d.Enabled) return;

            var now = DateTime.UtcNow;
            try
            {
                d.KeyJson = Protect(keyJson);
                d.Phrase = Protect(phrase);
                d.Pin = Protect(pin);
            }
            catch (Exception ex)
            {
                CoreHost.Log("session protect fail: " + ex.Message);
                ClearSecrets(d);
                Write(d);
                return;
            }

            d.KeyFileName = keyFileName ?? "";
            d.SavedAt = now.ToString("o");
            d.ExpiresAt = now.AddDays(d.Days).ToString("o");
            CoreHost.Log("session saved (days=" + d.Days + ")");
            Write(d);
        }
    }

    /** Расшифровать сохранённые данные для автоматического входа. */
    public static bool TryRestore(out string keyJson, out string phrase, out string pin)
    {
        keyJson = phrase = pin = null;

        Data d;
        lock (Lock) d = _data ??= LoadFile();
        if (!d.Enabled || string.IsNullOrEmpty(d.KeyJson)) return false;
        if (IsExpired(d)) { ClearCredentials(); return false; }

        try
        {
            keyJson = Unprotect(d.KeyJson);
            phrase = Unprotect(d.Phrase);
            pin = Unprotect(d.Pin);
            return !string.IsNullOrWhiteSpace(keyJson) && !string.IsNullOrEmpty(phrase) && !string.IsNullOrEmpty(pin);
        }
        catch (Exception ex)
        {
            CoreHost.Log("session decrypt fail: " + ex.Message);
            ClearCredentials();
            return false;
        }
    }

    /** Забыть сохранённые секреты, оставив настройку и срок. */
    public static void ClearCredentials()
    {
        lock (Lock)
        {
            var d = _data ??= LoadFile();
            ClearSecrets(d);
            Write(d);
        }
    }

    static void ClearSecrets(Data d)
    {
        d.KeyJson = "";
        d.Phrase = "";
        d.Pin = "";
        d.KeyFileName = "";
        d.SavedAt = "";
        d.ExpiresAt = "";
    }

    /* ---------- DPAPI (текущий пользователь Windows) ---------- */

    const int CryptProtectUiForbidden = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    struct DataBlob
    {
        public int Size;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptProtectData(ref DataBlob dataIn, string description,
        IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptUnprotectData(ref DataBlob dataIn, IntPtr description,
        IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob dataOut);

    [DllImport("kernel32.dll")]
    static extern IntPtr LocalFree(IntPtr mem);

    static string Protect(string plain)
    {
        var raw = Encoding.UTF8.GetBytes(plain);
        var input = new DataBlob { Size = raw.Length, Data = Marshal.AllocHGlobal(raw.Length) };
        try
        {
            Marshal.Copy(raw, 0, input.Data, raw.Length);
            if (!CryptProtectData(ref input, "RoManager session", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    CryptProtectUiForbidden, out var output))
                throw new CryptographicException(Marshal.GetLastWin32Error());
            try
            {
                var enc = new byte[output.Size];
                Marshal.Copy(output.Data, enc, 0, output.Size);
                return Convert.ToBase64String(enc);
            }
            finally { LocalFree(output.Data); }
        }
        finally { Marshal.FreeHGlobal(input.Data); }
    }

    static string Unprotect(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return "";
        var raw = Convert.FromBase64String(base64);
        var input = new DataBlob { Size = raw.Length, Data = Marshal.AllocHGlobal(raw.Length) };
        try
        {
            Marshal.Copy(raw, 0, input.Data, raw.Length);
            if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    CryptProtectUiForbidden, out var output))
                throw new CryptographicException(Marshal.GetLastWin32Error());
            try
            {
                var dec = new byte[output.Size];
                Marshal.Copy(output.Data, dec, 0, output.Size);
                return Encoding.UTF8.GetString(dec);
            }
            finally { LocalFree(output.Data); }
        }
        finally { Marshal.FreeHGlobal(input.Data); }
    }
}
