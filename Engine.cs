using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace RoManager;

static class Jsons
{
    public static readonly JsonSerializerOptions Opt = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        IncludeFields = true
    };
    public static string S(object o) => JsonSerializer.Serialize(o, Opt);
}

/* ------------------------------------------------------------- CRYPTO */

static class Crypto
{
    public const int LegacyIterations = 150_000;
    public const int DefaultIterations = 600_000;

    /* ---------- v2: PBKDF2-SHA256 + AES-256-GCM ---------- */

    public static string CheckHash(string salt, string phrase, string pin) // v1, только чтение старых ключей
    {
        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(
            Encoding.UTF8.GetBytes($"rom-v1|{salt}|{Normalize(phrase)}|{pin}")));
    }
    static string Normalize(string phrase)
    {
        var w = (phrase ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', w).ToLowerInvariant();
    }

    /** v2: один PBKDF2-проход; check = HMAC-SHA256(ключ, "rom-v2-check"). */
    public static (byte[] key, string check) DeriveKey(string fileSalt, string phrase, string pin, int iterations)
    {
        var pass = $"rom-v2|{fileSalt}|{Normalize(phrase)}|{pin}";
        using var kdf = new Rfc2898DeriveBytes(
            pass, Convert.FromBase64String(fileSalt), iterations, HashAlgorithmName.SHA256);
        var key = kdf.GetBytes(32);
        var check = Convert.ToBase64String(
            HMACSHA256.HashData(key, Encoding.UTF8.GetBytes("rom-v2-check")));
        return (key, check);
    }

    public static bool CheckEquals(string a, string b)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(a ?? ""), Convert.FromBase64String(b ?? ""));
        }
        catch { return false; }
    }

    static byte[] Aad(string keyId) => Encoding.UTF8.GetBytes("rom-v2|" + keyId);

    public static (byte[] nonce, byte[] tag, byte[] data) EncryptGcm(string plain, byte[] key, string keyId)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var data = Encoding.UTF8.GetBytes(plain);
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, data, data, tag, Aad(keyId));
        return (nonce, tag, data);
    }

    public static string DecryptGcm(string nonceB64, string tagB64, string dataB64, byte[] key, string keyId)
    {
        var nonce = Convert.FromBase64String(nonceB64);
        var tag = Convert.FromBase64String(tagB64);
        var data = Convert.FromBase64String(dataB64);
        var plain = new byte[data.Length];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, data, tag, plain, Aad(keyId));
        return Encoding.UTF8.GetString(plain);
    }

    /* ---------- v1: AES-256-CBC (совместимость) ---------- */

    public static byte[] DeriveLegacyKey(string fileSalt, string phrase, string pin, string check)
    {
        using var kdf = new Rfc2898DeriveBytes(
            $"{fileSalt}|{Normalize(phrase)}|{pin}|{check}",
            Convert.FromBase64String(fileSalt), LegacyIterations, HashAlgorithmName.SHA256);
        return kdf.GetBytes(32);
    }

    public static (byte[] iv, byte[] data) EncryptLegacy(string plain, string fileSalt, string phrase, string pin, string check)
    {
        var key = DeriveLegacyKey(fileSalt, phrase, pin, check);
        var iv = RandomNumberGenerator.GetBytes(16);
        using var aes = Aes.Create();
        aes.Key = key; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
        using var enc = aes.CreateEncryptor();
        var raw = Encoding.UTF8.GetBytes(plain);
        return (iv, enc.TransformFinalBlock(raw, 0, raw.Length));
    }

    public static string DecryptLegacy(string ivB64, string dataB64, string fileSalt, string phrase, string pin, string check)
    {
        var key = DeriveLegacyKey(fileSalt, phrase, pin, check);
        using var aes = Aes.Create();
        aes.Key = key; aes.IV = Convert.FromBase64String(ivB64);
        aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
        using var dec = aes.CreateDecryptor();
        var data = Convert.FromBase64String(dataB64);
        var raw = dec.TransformFinalBlock(data, 0, data.Length);
        return Encoding.UTF8.GetString(raw);
    }

    public static string Salt() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}

/* ------------------------------------------------------------ VAULT */

public sealed class VaultSession
{
    public string Salt = "";
    public string Check = "";
    public string Phrase = "";
    public string Pin = "";
    /** Ключ v2 (AES-GCM) держим в памяти, чтобы не гонять PBKDF2 при каждом сохранении. */
    public byte[] Key;
    public int Version = 1;
    public int Iterations = Crypto.LegacyIterations;
    public bool Ready => Phrase.Length > 0 && Pin.Length > 0;
    public void Clear()
    {
        Salt = Check = Phrase = Pin = "";
        if (Key != null) CryptographicOperations.ZeroMemory(Key);
        Key = null;
        Version = 1;
        Iterations = Crypto.LegacyIterations;
    }
}

static class Vault
{
    public static string Dir => AppContext.BaseDirectory;
    public static string FilePath => Path.Combine(Dir, "AccountData.json");

    public sealed class AccountRepo
    {
        public List<AccountInfo> Accounts = new();
        public UserSettings Settings = new();
    }
    public sealed class UserSettings
    {
        public int PresenceRateSec = 30;
        public int AutoRecheckMin = 15;
        public bool AutoCookieRefresh = true;
        public int JoinDelayMs = 2500;
        public bool AutoCloseLast = true;
        public bool MultiInstance = false;
        public bool UnlockFps = false;
        public int MaxFps = 120;
        public bool SharePresence = true;
        public bool UseBundledChrome = true;
        public bool DevLogMode = false;
    }

    static VaultSession S => Bridge.Session;
    static AccountRepo _repo = new();
    static bool _writable = true;   // нельзя перезаписывать чужое/нерасшифрованное хранилище

    public static (bool ok, string msg) TryUnlock(string keyJson, string phrase, string pin)
    {
        try
        {
            var doc = JsonDocument.Parse(keyJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("salt", out var saltEl)) return (false, "Неверный формат файла.");
            var salt = saltEl.GetString()!;
            var check = root.TryGetProperty("check", out var cEl) ? cEl.GetString() : null;
            if (string.IsNullOrEmpty(check))
                return (false, "Файл не содержит check-контрольной суммы.");

            var version = root.TryGetProperty("version", out var vEl) && vEl.TryGetInt32(out var v) ? v : 1;
            var iterations = root.TryGetProperty("iterations", out var iEl) && iEl.TryGetInt32(out var it)
                ? Math.Clamp(it, 100_000, 2_000_000)
                : Crypto.LegacyIterations;

            byte[] key = null;
            if (version >= 2)
            {
                var derived = Crypto.DeriveKey(salt, phrase, pin, iterations);
                if (!Crypto.CheckEquals(derived.check, check))
                    return (false, "Файл, фраза или PIN не совпадают.");
                key = derived.key;
            }
            else if (Crypto.CheckHash(salt, phrase, pin) != check)
                return (false, "Файл, фраза или PIN не совпадают.");

            _writable = true;
            var store = Load(salt, phrase, pin, check, key);
            if (_loadFailed)
            {
                if (key != null) CryptographicOperations.ZeroMemory(key);
                return (false, "Хранилище не расшифровывается этим ключом. Данные не изменены.");
            }
            _repo = store;
            S.Salt = salt; S.Check = check; S.Phrase = phrase.Trim(); S.Pin = pin;
            S.Key = key; S.Version = version; S.Iterations = iterations;
            return (true, "");
        }
        catch (Exception ex) { return (false, "Ошибка: " + ex.Message); }
    }

    static bool _loadFailed;

    static string DecryptStore(JsonElement doc, string salt, string phrase, string pin, string check, byte[] key)
    {
        var dataVersion = doc.TryGetProperty("version", out var vEl) && vEl.TryGetInt32(out var v) ? v : 1;
        if (dataVersion >= 2)
        {
            if (key == null) throw new CryptographicException("Для этого хранилища нужен ключ версии 2.");
            return Crypto.DecryptGcm(
                doc.GetProperty("nonce").GetString(),
                doc.GetProperty("tag").GetString(),
                doc.GetProperty("data").GetString(),
                key, check);
        }
        return Crypto.DecryptLegacy(
            doc.GetProperty("iv").GetString(),
            doc.GetProperty("data").GetString(),
            salt, phrase, pin, check);
    }

    static AccountRepo Load(string salt, string phrase, string pin, string check, byte[] key)
    {
        _loadFailed = false;
        if (!File.Exists(FilePath) && !File.Exists(FilePath + ".bak"))
            return new AccountRepo();

        foreach (var path in new[] { FilePath, FilePath + ".bak" })
        {
            try
            {
                if (!File.Exists(path)) continue;
                var doc = JsonDocument.Parse(File.ReadAllText(path));
                var kid = doc.RootElement.GetProperty("keyId").GetString();
                if (kid != check)
                {
                    if (path == FilePath)
                    {
                        // хранилище принадлежит другому ключу — писать в него нельзя
                        _writable = false;
                        return new AccountRepo();
                    }
                    continue;
                }
                var json = DecryptStore(doc.RootElement, salt, phrase, pin, check, key);
                if (path != FilePath) Bridge.Log("vault restored from .bak");
                return JsonSerializer.Deserialize<AccountRepo>(json, Jsons.Opt) ?? new AccountRepo();
            }
            catch (Exception ex)
            {
                Bridge.Log($"vault load fail ({Path.GetFileName(path)}): {ex.Message}");
            }
        }
        _loadFailed = true;
        return new AccountRepo();
    }

    public static void Save()
    {
        if (S?.Ready != true) return;
        if (!_writable) { Bridge.Log("vault save skipped (not writable)"); return; }
        try
        {
            // резервная копия предыдущего состояния (если там были аккаунты)
            try
            {
                if (File.Exists(FilePath) && _repo.Accounts.Count > 0)
                    File.Copy(FilePath, FilePath + ".bak", true);
            }
            catch { }
            var payload = Jsons.S(_repo);
            object obj;
            if (S.Version >= 2 && S.Key != null)
            {
                var (nonce, tag, data) = Crypto.EncryptGcm(payload, S.Key, S.Check);
                obj = new
                {
                    version = 2,
                    alg = "AES-256-GCM",
                    kdf = "PBKDF2-SHA256",
                    iterations = S.Iterations,
                    keyId = S.Check,
                    nonce = Convert.ToBase64String(nonce),
                    tag = Convert.ToBase64String(tag),
                    data = Convert.ToBase64String(data)
                };
            }
            else
            {
                var (iv, data) = Crypto.EncryptLegacy(payload, S.Salt, S.Phrase, S.Pin, S.Check);
                obj = new
                {
                    version = 1,
                    alg = "AES-256-CBC",
                    iterations = Crypto.LegacyIterations,
                    keyId = S.Check,
                    iv = Convert.ToBase64String(iv),
                    data = Convert.ToBase64String(data)
                };
            }
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, Jsons.S(obj));
            File.Move(tmp, FilePath, true);
        }
        catch (Exception ex) { Bridge.PushStatus("Ошибка сохранения: " + ex.Message, "danger"); }
    }

    public static List<AccountInfo> All() => _repo.Accounts;
    public static UserSettings Settings => _repo.Settings;
    public static AccountInfo Find(long id) => _repo.Accounts.FirstOrDefault(a => a.Id == id);

    public static AccountInfo Upsert(AccountInfo a)
    {
        var ex = _repo.Accounts.FirstOrDefault(x => x.UserId == a.UserId && a.UserId > 0);
        if (ex != null) { ex.Cookie = a.Cookie; ex.Name = a.Name; return ex; }
        a.Id = (long)(DateTime.UtcNow.Ticks % 1000000) + _repo.Accounts.Count + 1;
        _repo.Accounts.Add(a);
        Save();
        return a;
    }
    public static void Remove(IEnumerable<long> ids)
    {
        _repo.Accounts.RemoveAll(a => ids.Contains(a.Id));
        Save();
    }
    public static string KeyFileResave()
    {
        if (S?.Ready != true) return null;
        if (S.Version >= 2)
            return Jsons.S(new
            {
                version = 2,
                alg = "AES-256-GCM",
                kdf = "PBKDF2-SHA256",
                iterations = S.Iterations,
                salt = S.Salt,
                check = S.Check,
                created = DateTime.UtcNow.ToString("o"),
                note = "RoManager key"
            });
        return Jsons.S(new { version = 1, salt = S.Salt, check = S.Check, created = DateTime.UtcNow.ToString("o"), note = "RoManager key" });
    }
    public static string GetKeyFileContent() => KeyFileResave();
}

/* ------------------------------------------------------------ MODEL */

public class AccountInfo
{
    public long Id;
    public string Name = "";
    public long UserId;
    public string Cookie = "";
    public string LastOnline = "—";
    public bool DataOk = true;
    public bool Online;
    public string LastUpdated = "";
    public string LastPlaceId = "";
    public string LastJobId = "";
    public string LastGameName = "";
    /* клиент запущен менеджером прямо сейчас (не сохраняется) */
    [System.Text.Json.Serialization.JsonIgnore] public bool LaunchTracked;
}

/* --------------------------------------------------------- ROBLOX API */

static class Roblox
{
    public static HttpClient Http;
    static readonly Dictionary<string, HttpClient> _byCookie = new();
    static System.Threading.Timer _presenceT, _checkT;
    static readonly object _lock = new();
    static Mutex _multiMutex;

    public static void Init()
    {
        if (Http != null) return;
        var h = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });
        h.DefaultRequestHeaders.UserAgent.ParseAdd("RoManager/1.0");
        h.Timeout = TimeSpan.FromSeconds(15);
        Http = h;
        StartTimers();
    }
    public static void Shutdown()
    {
        try { _presenceT?.Dispose(); _checkT?.Dispose(); } catch { }
        try
        {
            if (_multiMutex != null)
            {
                try { _multiMutex.ReleaseMutex(); } catch { }
                _multiMutex.Dispose();
                _multiMutex = null;
            }
        }
        catch { }
    }

    /* Multi Roblox: держим системный мьютекс, который проверяет клиент.
       Работает только если Roblox был закрыт в момент включения. */
    public static bool ApplyMultiInstance()
    {
        var enabled = Vault.Settings.MultiInstance;
        if (enabled)
        {
            if (_multiMutex != null) return true;
            try
            {
                var m = new Mutex(true, "ROBLOX_singletonMutex", out var created);
                if (!created && !m.WaitOne(TimeSpan.Zero, true))
                {
                    m.Dispose();
                    Bridge.Log("multi-instance: mutex is busy");
                    return false;
                }
                _multiMutex = m;
                Bridge.Log("multi-instance: enabled");
                return true;
            }
            catch (Exception ex)
            {
                Bridge.Log("multi-instance EX: " + ex.Message);
                return false;
            }
        }
        if (_multiMutex != null)
        {
            try { _multiMutex.ReleaseMutex(); } catch { }
            try { _multiMutex.Dispose(); } catch { }
            _multiMutex = null;
            Bridge.Log("multi-instance: disabled");
        }
        return true;
    }

    public static HttpClient For(string cookie)
    {
        lock (_lock)
        {
            if (!_byCookie.TryGetValue(cookie, out var c))
            {
                var h = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });
                h.DefaultRequestHeaders.UserAgent.ParseAdd("RoManager/1.0");
                var req = new HttpRequestMessage();
                h.Timeout = TimeSpan.FromSeconds(15);
                c = h;
                _byCookie[cookie] = c;
            }
            return c;
        }
    }

    public static HttpRequestMessage Req(HttpMethod m, string url, string cookie, string jsonBody = null)
    {
        var r = new HttpRequestMessage(m, url);
        if (!string.IsNullOrEmpty(cookie)) r.Headers.TryAddWithoutValidation("Cookie", $".ROBLOSECURITY={cookie}");
        if (jsonBody != null)
        {
            r.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }
        r.Headers.TryAddWithoutValidation("Referer", "https://www.roblox.com");
        return r;
    }

    /* запросы, которым нужен конкретный Referer (auth-ticket) */
    public static HttpRequestMessage AuthReq(string url, string cookie)
    {
        var r = Req(new HttpMethod("POST"), url, cookie, "{}");
        r.Headers.Remove("Referer");
        r.Headers.Referrer = new Uri("https://www.roblox.com/games/2753915549/Blox-Fruits");
        r.Headers.TryAddWithoutValidation("Origin", "https://www.roblox.com");
        return r;
    }

    /* Validate cookie -> user info */
    public static (bool ok, string name, long uid, string err) ValidateCookie(string cookie)
    {
        if (string.IsNullOrWhiteSpace(cookie)) return (false, null, 0, "Пустой cookie");
        try
        {
            var r = Req(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated", cookie);
            var resp = Http.SendAsync(r).GetAwaiter().GetResult();
            var txt = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode) return (false, null, 0, $"HTTP {(int)resp.StatusCode}");
            var j = JsonDocument.Parse(txt).RootElement;
            return (true, j.GetProperty("name").GetString(), j.GetProperty("id").GetInt64(), "");
        }
        catch (Exception ex) { return (false, null, 0, ex.Message); }
    }

    public static (long id, string err) GetUserId(string username)
    {
        try
        {
            var r = Req(HttpMethod.Post, "https://users.roblox.com/v1/usernames/users", null,
                Jsons.S(new { usernames = new[] { username }, excludeBannedUsers = false }));
            var resp = Http.SendAsync(r).GetAwaiter().GetResult();
            var txt = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var arr = JsonDocument.Parse(txt).RootElement.GetProperty("data");
            if (arr.GetArrayLength() == 0) return (0, "Игрок не найден");
            return (arr[0].GetProperty("id").GetInt64(), "");
        }
        catch (Exception ex) { return (0, ex.Message); }
    }

    public static string GetPlaceName(string placeId)
    {
        try
        {
            var r = Req(HttpMethod.Get, $"https://apis.roblox.com/universes/v1/places/{placeId}/universe", null);
            var resp = Http.SendAsync(r).GetAwaiter().GetResult();
            var txt = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var uniId = JsonDocument.Parse(txt).RootElement.GetProperty("universeId").GetInt64();
            var r2 = Req(HttpMethod.Get, $"https://games.roblox.com/v1/games?universeIds={uniId}", null);
            var txt2 = Http.SendAsync(r2).GetAwaiter().GetResult().Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonDocument.Parse(txt2).RootElement.GetProperty("data")[0].GetProperty("name").GetString();
        }
        catch { return null; }
    }

    public static (string ticket, string err) GetAuthTicket(string cookie)
    {
        try
        {
            var c = For(cookie);
            const string ticketUrl = "https://auth.roblox.com/v1/authentication-ticket";

            // CSRF берём из ответа на "пустой" запрос (403 + x-csrf-token)
            string csrf = null;
            try
            {
                var probe = AuthReq(ticketUrl, cookie);
                var pr = c.SendAsync(probe).GetAwaiter().GetResult();
                if (pr.Headers.TryGetValues("x-csrf-token", out var pv)) csrf = pv.FirstOrDefault();
            }
            catch { }

            var r = AuthReq(ticketUrl, cookie);
            if (csrf != null) r.Headers.TryAddWithoutValidation("x-csrf-token", csrf);

            var resp = c.SendAsync(r).GetAwaiter().GetResult();
            var txt = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            // Roblox отдаёт билет в заголовке rbx-authentication-ticket
            string ticket = null;
            foreach (var hn in new[] { "rbx-authentication-ticket", "authentication-ticket", "x-authentication-ticket" })
                if (resp.Headers.TryGetValues(hn, out var vals)) { ticket = vals.FirstOrDefault(); break; }

            if (string.IsNullOrEmpty(ticket) && !string.IsNullOrWhiteSpace(txt))
            {
                try
                {
                    var j = JsonDocument.Parse(txt).RootElement;
                    foreach (var p in new[] { "authenticationTicket", "AuthenticationTicket", "ticket" })
                        if (j.TryGetProperty(p, out var el)) { ticket = el.GetString(); break; }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(ticket)) return (ticket, "");
            if (!resp.IsSuccessStatusCode)
                return (null, $"HTTP {(int)resp.StatusCode}: {txt.Truncate(200)}");
            return (null, "сервер не вернул билет: " + txt.Truncate(200));
        }
        catch (Exception ex)
        {
            Bridge.Log("auth-ticket EX: " + ex.Message);
            return (null, ex.Message);
        }
    }

    static long _trackerId;
    static long TrackerId()
    {
        if (_trackerId == 0)
            _trackerId = Math.Abs(BitConverter.ToInt64(RandomNumberGenerator.GetBytes(8), 0)) % 9000000000000000 + 1000000000000000;
        return _trackerId;
    }

    /* presence игрока -> (placeId, jobId) чтобы подключиться «К игроку» */
    public static (string placeId, string jobId, string err) GetUserPlace(string cookie, long userId)
    {
        try
        {
            var r = Req(new HttpMethod("POST"), "https://presence.roblox.com/v1/presence/users", cookie,
                Jsons.S(new { userIds = new[] { userId } }));
            var resp = Http.SendAsync(r).GetAwaiter().GetResult();
            var txt = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var arr = JsonDocument.Parse(txt).RootElement.GetProperty("userPresences");
            if (arr.GetArrayLength() == 0) return (null, null, "Игрок не найден");
            var p = arr[0];
            int type = p.TryGetProperty("userPresenceType", out var t) ? t.GetInt32() : 0;
            if (type != 2) return (null, null, "Игрок сейчас не в игре");
            string placeId = p.TryGetProperty("placeId", out var pl) ? pl.GetInt64().ToString() : null;
            string jobId = p.TryGetProperty("gameId", out var gj) ? gj.GetString() : null;
            if (string.IsNullOrEmpty(placeId)) return (null, null, "Не удалось определить игру игрока");
            return (placeId, jobId, "");
        }
        catch (Exception ex) { return (null, null, ex.Message); }
    }

    public static void Launch(AccountInfo acc, string placeId, string code, string jobId, bool homeMode)
    {
        var btid = TrackerId();
        string launchUrl = null;
        if (!homeMode)
        {
            if (!string.IsNullOrEmpty(code))
            {
                launchUrl = "https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestPrivateGame" +
                            $"&placeId={placeId}&accessCode={Uri.EscapeDataString(code)}&linkCode={Uri.EscapeDataString(code)}";
            }
            else
            {
                // формат как в оригинале RAM
                launchUrl = "https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestGame" +
                            $"&browserTrackerId={btid}&placeId={placeId}" +
                            (string.IsNullOrEmpty(jobId) ? "" : $"&gameId={jobId}") +
                            "&isPlayTogetherGame=false";
            }
        }

        var (ticket, err) = GetAuthTicket(acc.Cookie);
        if (ticket == null)
        {
            Bridge.Log("auth-ticket failed: " + err);
            throw new Exception("Не удалось получить auth-ticket: " + err);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var uri = "roblox-player://1/1+launchmode:play+gameinfo:" + ticket +
                  "+launchtime:" + now +
                  "+browsertrackerid:" + btid +
                  (launchUrl != null ? "+placelauncherurl:" + launchUrl : "") +
                  "+robloxLocale:en_us+gameLocale:en_us";
        Bridge.Log("launch: " + (homeMode ? "home" : launchUrl));

        if (Vault.Settings.AutoCloseLast) CloseRoblox();
        if (Vault.Settings.UnlockFps) PatchFps();

        var before = ClientPids();
        try
        {
            Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true });
        }
        catch (Win32Exception ex)
        {
            throw new Exception("Roblox не установлен или протокол roblox-player не зарегистрирован: " + ex.Message);
        }
        WatchClient(acc, before);
    }

    /* ---------- учёт «последний онлайн» по факту запуска/закрытия клиента ---------- */
    static HashSet<int> ClientPids()
    {
        var set = new HashSet<int>();
        foreach (var p in Process.GetProcessesByName("RobloxPlayerBeta"))
        {
            try { set.Add(p.Id); } catch { }
            try { p.Dispose(); } catch { }
        }
        return set;
    }

    static async void WatchClient(AccountInfo acc, HashSet<int> before)
    {
        try
        {
            Process client = null;
            for (int i = 0; i < 180 && client == null; i++)
            {
                await Task.Delay(500);
                foreach (var p in Process.GetProcessesByName("RobloxPlayerBeta"))
                {
                    if (before.Contains(p.Id)) { p.Dispose(); continue; }
                    client = p;
                    break;
                }
            }
            if (client == null) { Bridge.Log("watch: client not found for " + acc.Name); return; }

            Bridge.Log($"watch: client pid={client.Id} started ({acc.Name})");
            acc.Online = true;
            acc.LaunchTracked = true;
            Bridge.PushAccounts();

            try { await client.WaitForExitAsync(); } catch { }
            try { client.Dispose(); } catch { }

            acc.Online = false;
            acc.LaunchTracked = false;
            Vault.Save();
            Bridge.PushAccounts();
            Bridge.Log($"watch: client closed ({acc.Name})");
        }
        catch (Exception ex) { Bridge.Log("watch EX: " + ex.Message); }
    }

    static void CloseRoblox()
    {
        try
        {
            foreach (var p in Process.GetProcessesByName("RobloxPlayerBeta"))
            {
                try { p.Kill(); p.Dispose(); } catch { }
            }
        }
        catch { }
    }

    static void PatchFps()
    {
        try
        {
            var local = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Roblox", "Versions");
            if (!Directory.Exists(local)) return;
            foreach (var dir in Directory.GetDirectories(local, "version-*"))
            {
                var f = Path.Combine(dir, "ClientSettings", "ClientAppSettings.json");
                Directory.CreateDirectory(Path.GetDirectoryName(f));
                var j = File.Exists(f) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(f)) : new();
                j["DFIntTaskSchedulerTargetFps"] = Vault.Settings.MaxFps.ToString();
                File.WriteAllText(f, Jsons.S(j));
            }
        }
        catch { }
    }

    public static void RegisterLaunch(AccountInfo a)
    {
        a.Online = true;
        Bridge.PushAccounts();
        Vault.Save();
    }
    public static void StartTimers()
    {
        var presenceSec = Math.Clamp(Vault.Settings.PresenceRateSec, 5, 600);
        if (Vault.Settings.SharePresence)
            _presenceT = new System.Threading.Timer(async o => await PresenceTick(), null,
                TimeSpan.FromSeconds(presenceSec),
                TimeSpan.FromSeconds(presenceSec));
        var checkMin = Math.Clamp(Vault.Settings.AutoRecheckMin, 1, 1440);
        if (Vault.Settings.AutoCookieRefresh)
            _checkT = new System.Threading.Timer(async o => await CheckTick(), null,
                TimeSpan.FromMinutes(checkMin),
                TimeSpan.FromMinutes(checkMin));
    }
    public static void RestartTimers()
    {
        try { _presenceT?.Dispose(); _presenceT = null; _checkT?.Dispose(); _checkT = null; } catch { }
        StartTimers();
    }
    static async Task PresenceTick()
    {
        var list = Vault.All();
        if (list.Count == 0 || !Vault.Settings.SharePresence) return;
        try
        {
            var acc = list[0];
            // Реальный last online + presence
            var r = Req(new HttpMethod("POST"), "https://presence.roblox.com/v1/presence/users", acc.Cookie,
                Jsons.S(new { userIds = list.Select(a => a.UserId).Distinct().ToArray() }));
            var resp = await Http.SendAsync(r);
            var txt = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return;
            var arr = JsonDocument.Parse(txt).RootElement.GetProperty("userPresences");
            var nowLocal = DateTime.Now;
            foreach (var u in arr.EnumerateArray())
            {
                var uid = u.GetProperty("userId").GetInt64();
                var type = u.GetProperty("userPresenceType").GetInt32();
                var accT = list.FirstOrDefault(a => a.UserId == uid);
                if (accT == null) continue;
                if (accT.LaunchTracked) continue; // сейчас играем через менеджер — не трогаем
                accT.Online = type > 0;
            }
            Bridge.PushAccounts();
        }
        catch { }
    }
    static async Task CheckTick()
    {
        var list = Vault.All();
        foreach (var acc in list)
        {
            var r = await Task.Run(() => ValidateCookie(acc.Cookie));
            acc.DataOk = r.ok;
            if (!r.ok) Bridge.PushAccounts();
        }
        Bridge.PushAccounts();
        Vault.Save();
    }
}

/* ----------------------------------------------------- CHROME LOGIN (CDP) */

static class ChromeLogin
{
    static string FindBrowser()
    {
        // 1. портативный рядом (можно отключить в настройках)
        if (Vault.Settings.UseBundledChrome)
        {
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 4 && dir != null; i++, dir = Path.GetDirectoryName(dir))
            {
                var c = Path.Combine(dir, "chrome-win64", "chrome.exe");
                if (File.Exists(c)) return c;
            }
        }
        // 2. реестр App Paths - Chrome приоритетно
        string chromePath = null;
        try
        {
            var v = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe")?.GetValue(null) as string
                 ?? Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe")?.GetValue(null) as string;
            if (v != null && File.Exists(v)) chromePath = v;
        }
        catch { }
        if (chromePath != null) return chromePath;
        // 3. Program Files
        foreach (var c in new[] {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google\\Chrome\\Application\\chrome.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google\\Chrome\\Application\\chrome.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google\\Chrome\\Application\\chrome.exe") })
            if (File.Exists(c)) return c;
        // 4. Edge как последний fallback
        foreach (var c in new[] {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft\\Edge\\Application\\msedge.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft\\Edge\\Application\\msedge.exe") })
            if (File.Exists(c)) return c;
        return null;
    }

    static int FreePort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start(); var p = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop(); return p;
    }

    /* Запуск временного профиля Chrome + слежение за .ROBLOSECURITY через БРАУЗЕРНЫЙ
       эндпоинт DevTools (устойчив к навигациям/вкладкам - структура /json/version). */
    public static async Task<(string cookie, string name, long uid)> StartAsync(
        Action<string> status, string injectCookie = null)
    {
        var exe = FindBrowser();
        if (exe == null) { status?.Invoke("Браузер Chrome/Edge не найден"); return (null, null, 0); }
        Bridge.Log($"chrome exe={exe}");
        int port = FreePort();
        var profile = Path.Combine(Path.GetTempPath(), "rom_chrome_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        var args = $"--remote-debugging-port={port} --remote-allow-origins=* " +
                   $"--user-data-dir=\"{profile}\" --no-first-run --no-default-browser-check --window-size=1100,780";
        var psi = new ProcessStartInfo(exe, args);
        var proc = Process.Start(psi);
        Bridge.Log($"chrome pid={proc.Id} port={port}");

        string browserWs = null;
        try
        {
            using (var http = new HttpClient())
            {
                for (int i = 0; i < 40 && browserWs == null; i++)
                {
                    await Task.Delay(1000);
                    if (proc.HasExited) { Bridge.Log("chrome exited early"); return (null, null, 0); }
                    try
                    {
                        var ver = await http.GetStringAsync($"http://127.0.0.1:{port}/json/version");
                        browserWs = JsonDocument.Parse(ver).RootElement.GetProperty("webSocketDebuggerUrl").GetString();
                    }
                    catch { }
                }
            }
            if (browserWs == null) { status?.Invoke("DevTools недоступен"); return (null, null, 0); }
            Bridge.Log("browser ws ok");

            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(browserWs), CancellationToken.None);
            int idSeq = 0;

            async Task<JsonElement> Cdp(string method, object cargs = null, int timeoutMs = 10000)
            {
                var id = ++idSeq;
                var send = Jsons.S(new { id, method, @params = cargs ?? new { } });
                var buf = Encoding.UTF8.GetBytes(send);
                await ws.SendAsync(buf, WebSocketMessageType.Text, true, CancellationToken.None);
                var deadline = Environment.TickCount64 + timeoutMs;
                while (Environment.TickCount64 < deadline)
                {
                    var seg = new byte[512 * 1024];
                    var recvTask = ws.ReceiveAsync(seg, CancellationToken.None);
                    var t = await Task.WhenAny(recvTask, Task.Delay(500));
                    if (t != recvTask) { if (Environment.TickCount64 > deadline) break; continue; }
                    var r = recvTask.Result;
                    if (r.MessageType != WebSocketMessageType.Text) continue;
                    var memo = new MemoryStream();
                    memo.Write(seg, 0, r.Count);
                    while (!r.EndOfMessage)
                    {
                        r = await ws.ReceiveAsync(seg, CancellationToken.None);
                        memo.Write(seg, 0, r.Count);
                    }
                    var msg = Encoding.UTF8.GetString(memo.ToArray(), 0, (int)memo.Length);
                    try
                    {
                        var j = JsonDocument.Parse(msg).RootElement;
                        if (j.TryGetProperty("id", out var idel) && idel.GetInt64() == id)
                            return j.GetProperty("result");
                    }
                    catch { }
                }
                throw new TimeoutException(method);
            }

            if (!string.IsNullOrEmpty(injectCookie))
            {
                await Cdp("Storage.setCookie", new
                {
                    cookies = new[]
                    {
                        new
                        {
                            name = ".ROBLOSECURITY",
                            value = injectCookie.Trim(),
                            domain = ".roblox.com",
                            path = "/",
                            secure = true,
                            httpOnly = true
                        }
                    }
                });
                await Cdp("Target.createTarget", new { url = "https://www.roblox.com/home" });
            }
            else
            {
                await Cdp("Target.createTarget", new { url = "https://www.roblox.com/login" });
            }
            Bridge.Log("login tab opened");

            string cookie = null;
            int tries = 0;
            while (cookie == null && !proc.HasExited && tries < 900)
            {
                tries++;
                await Task.Delay(1200);
                try
                {
                    var res = await Cdp("Storage.getCookies", null, 8000);
                    var arr = res.GetProperty("cookies");
                    foreach (var c in arr.EnumerateArray())
                    {
                        if (c.GetProperty("name").GetString() == ".ROBLOSECURITY")
                        {
                            var v = c.GetProperty("value").GetString();
                            if (!string.IsNullOrEmpty(v) && v.Length > 50) { cookie = v; break; }
                        }
                    }
                }
                catch (Exception ex) { if (tries % 25 == 0) Bridge.Log("cookie poll fail: " + ex.Message); }
                if (cookie != null) { Bridge.Log("cookie captured, tries=" + tries); status?.Invoke("Куки получены, проверяем..."); }
            }

            if (cookie == null && proc.HasExited) { Bridge.Log("proc exited w/o cookie"); status?.Invoke("Окно закрыто без входа"); return (null, null, 0); }
            try { await Cdp("Browser.close", null, 4000); } catch { }
            try { proc.Kill(); } catch { }
            try { if (!proc.WaitForExit(3000)) proc.Kill(); } catch { }
            try { Directory.Delete(profile, true); } catch { }
            return (cookie, null, 0);
        }
        catch (Exception ex)
        {
            Bridge.Log("ChromeLogin EX: " + ex);
            try { proc.Kill(); } catch { }
            try { Directory.Delete(profile, true); } catch { }
            status?.Invoke("Ошибка браузера: " + ex.Message);
            return (null, null, 0);
        }
        finally
        {
            proc?.Dispose();
        }
    }
}

static class Ext
{
    public static string Truncate(this string s, int n) => s == null || s.Length <= n ? s : s.Substring(0, n) + "...";
}