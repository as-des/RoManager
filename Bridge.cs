using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;

namespace RoManager;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public sealed class Bridge
{
    static CoreWebView2 _wv2;
    public static VaultSession Session;

    public void Attach(CoreWebView2 wv2)
    {
        _wv2 = wv2;
        Session = new VaultSession();
        wv2.AddHostObjectToScript("rom", this);
        Roblox.Init();
    }

    /* Версия приложения для нижней панели и окна истории обновлений. */
    public string GetVersion()
    {
        var v = typeof(Bridge).Assembly.GetName().Version;
        if (v == null) return "2.1.0";
        return $"{v.Major}.{v.Minor}.{(v.Build >= 0 ? v.Build : 0)}";
    }

    /* ---------- язык интерфейса (ui.json, доступен до входа) ---------- */
    public string GetUiLanguage() => Loc.Language;

    public void SetUiLanguage(string lang)
    {
        Loc.Set(lang);
        Loc.ApplyTitles();
        var js = $"window.__applyLanguage && window.__applyLanguage({Json(Loc.Language)});";
        Push(js);
        SettingsFrame.PushSettings(js);
    }

    /* ---------- встроенный логин Roblox (как в оригинале RAM) ---------- */
    public void OpenBrowserLogin()
    {
        if (Session == null || !Session.Ready) { PushStatus(Loc.T("Сначала войдите в приложение", "Sign in first"), "danger"); return; }
        Log("OpenBrowserLogin");
        PushStatus(Loc.T("Открываю окно входа Roblox…", "Opening the Roblox sign-in window…"), "busy");
        MainFrame.Inst.RunOnUi(() =>
        {
            if (BrowserLoginFrame.Inst == null || BrowserLoginFrame.Inst.IsDisposed)
            {
                var f = new BrowserLoginFrame();
                f.Show(MainFrame.Inst);
            }
            else { BrowserLoginFrame.Inst.Activate(); }
        });
    }

    /* Проверяет cookie, добавляет аккаунт, обновляет список. Возвращает успех. */
    public bool TryAddAccountByCookie(string cookie)
    {
        if (string.IsNullOrWhiteSpace(cookie) || cookie.Length < 50) return false;
        var v = Roblox.ValidateCookie(cookie);
        if (!v.ok) { Log("cookie invalid: " + v.err); PushStatus(Loc.T("Cookie недействителен: ", "Invalid cookie: ") + v.err, "danger"); return false; }
        var acc = new AccountInfo
        {
            Name = v.name,
            UserId = v.uid,
            Cookie = cookie,
            DataOk = true,
            LastUpdated = DateTime.Now.ToString("dd.MM.yyyy HH:mm")
        };
        Vault.Upsert(acc);
        Vault.Save();
        LogDev($"account added: {v.name} ({v.uid})");
        PushStatus(Loc.F("Аккаунт {0} добавлен", "Account {0} added", v.name), "success");
        PushAccounts();
        Push("window.__enterDashboard();");
        return true;
    }

    /* Открыть окно «Обновить данные»: профиль тоже свежий, cookie подставляется в него */
    public void OpenBrowserRefresh(long id)
    {
        if (Session == null || !Session.Ready) { PushStatus(Loc.T("Сначала войдите в приложение", "Sign in first"), "danger"); return; }
        var acc = Vault.Find(id);
        if (acc == null) { PushStatus(Loc.T("Аккаунт не найден", "Account not found"), "danger"); return; }
        Log("OpenBrowserRefresh " + acc.Name);
        PushStatus(Loc.F("Обновление данных {0}…", "Refreshing data for {0}…", acc.Name), "busy");
        var cookie = acc.Cookie;
        MainFrame.Inst.RunOnUi(() =>
        {
            if (BrowserLoginFrame.Inst != null && !BrowserLoginFrame.Inst.IsDisposed)
                BrowserLoginFrame.Inst.Close();
            var f = new BrowserLoginFrame(injectCookie: cookie, refreshId: id);
            f.Show(MainFrame.Inst);
        });
    }

    /* Пришла свежая cookie для уже существующего аккаунта */
    public bool TryRefreshAccountByCookie(long id, string cookie)
    {
        var acc = Vault.Find(id);
        if (acc == null) return false;
        if (string.IsNullOrWhiteSpace(cookie) || cookie.Length < 50) return false;
        var v = Roblox.ValidateCookie(cookie);
        if (!v.ok) { Log("refresh invalid: " + v.err); PushStatus(Loc.T("Cookie недействителен: ", "Invalid cookie: ") + v.err, "danger"); return false; }
        if (v.uid != acc.UserId) { PushStatus(Loc.T("Вошёл другой аккаунт — обновление отменено", "A different account signed in — refresh cancelled"), "danger"); return false; }
        acc.Cookie = cookie;
        acc.Name = v.name;
        acc.DataOk = true;
        acc.Online = false;
        acc.LastUpdated = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        Vault.Save();
        Log("account refreshed: " + v.name);
        PushStatus(Loc.F("Данные {0} обновлены", "Data for {0} updated", v.name), "success");
        PushAccounts();
        return true;
    }


    /* ---------- window ---------- */
    public void Drag() => MainFrame.Inst.DragWindow();
    public void Minimize() => MainFrame.Inst.Minimize();
    public void ToggleMax() => MainFrame.Inst.ToggleMax();
    public void CloseApp() => MainFrame.Inst.Quit();

    /* ---------- окно настроек ---------- */
    public void OpenSettings()
    {
        Log("OpenSettings called");
        MainFrame.Inst.RunOnUi(() =>
        {
            if (SettingsFrame.Inst == null || SettingsFrame.Inst.IsDisposed)
            {
                var f = new SettingsFrame();
                f.Show(MainFrame.Inst);
            }
            else { SettingsFrame.Inst.Activate(); SettingsFrame.Inst.Focus(); }
        });
    }
    public void DragSettings() => SettingsFrame.Inst?.DragForm();
    public void CloseSettingsWindow()
    {
        MainFrame.Inst.RunOnUi(() => SettingsFrame.Inst?.Close());
    }

    /* ---------- изменение размера безрамного окна ----------
       JS при pointerdown на краю/углу вызывает BeginResize(edge).
       Дальше C# сам следит за курсором таймером — окно не «дёргается»,
       даже если WebView2 теряет pointer-события при ресайзе. */
    static System.Windows.Forms.Timer _resizeTimer;
    static string _resizeEdge = "";
    static Point _resizeStartCursor;
    static Rectangle _resizeStartBounds;

    static readonly string[] ResizeEdges =
        { "left", "right", "top", "bottom", "topleft", "topright", "bottomleft", "bottomright" };

    public void BeginResize(string edge)
    {
        var f = MainFrame.Inst;
        if (f == null || f.WindowState != FormWindowState.Normal) return;
        if (edge == null || !ResizeEdges.Contains(edge)) return;

        _resizeEdge = edge;
        _resizeStartCursor = Cursor.Position;
        _resizeStartBounds = f.Bounds;

        _resizeTimer ??= new System.Windows.Forms.Timer { Interval = 15 };
        _resizeTimer.Stop();
        _resizeTimer.Tick -= ResizeTick;
        _resizeTimer.Tick += ResizeTick;
        _resizeTimer.Start();
    }

    public void EndResize()
    {
        _resizeTimer?.Stop();
        _resizeEdge = "";
    }

    static void ResizeTick(object sender, EventArgs e)
    {
        var f = MainFrame.Inst;
        if (f == null || _resizeEdge.Length == 0) { _resizeTimer?.Stop(); return; }
        if ((Control.MouseButtons & MouseButtons.Left) == 0) { _resizeTimer?.Stop(); _resizeEdge = ""; return; }

        var p = Cursor.Position;
        ResizeTo(f, p.X - _resizeStartCursor.X, p.Y - _resizeStartCursor.Y);
    }

    static void ResizeTo(MainFrame f, int dx, int dy)
    {
        var b = _resizeStartBounds;
        int left = b.Left, top = b.Top, right = b.Right, bottom = b.Bottom;
        int minW = Math.Max(400, f.MinimumSize.Width);
        int minH = Math.Max(300, f.MinimumSize.Height);

        if (_resizeEdge.Contains("left")) left = Math.Min(b.Left + dx, right - minW);
        if (_resizeEdge.Contains("right")) right = Math.Max(b.Right + dx, left + minW);
        if (_resizeEdge.Contains("top")) top = Math.Min(b.Top + dy, bottom - minH);
        if (_resizeEdge.Contains("bottom")) bottom = Math.Max(b.Bottom + dy, top + minH);

        if (right - left < minW) right = left + minW;
        if (bottom - top < minH) bottom = top + minH;

        f.Bounds = Rectangle.FromLTRB(left, top, right, bottom);
    }

    /** Диагностика/тест: изменить размер как за правый нижний угол. */
    internal static void ResizeBy(int dx, int dy)
    {
        var f = MainFrame.Inst;
        if (f == null) return;
        _resizeEdge = "bottomright";
        _resizeStartBounds = f.Bounds;
        ResizeTo(f, dx, dy);
        _resizeEdge = "";
    }

    public static string Json(object o) => Jsons.S(o);
    public static void Push(string js)
    {
        try
        {
            var wv = _wv2;
            MainFrame.Inst.RunOnUi(() =>
            {
                try { _ = wv?.ExecuteScriptAsync(js); }
                catch (Exception ex) { Log("PUSH inner FAIL: " + ex.Message); }
            });
            LogDev("PUSH ui-dispatch " + js.Substring(0, Math.Min(50, js.Length)));
        }
        catch (Exception ex)
        {
            Log("PUSH FAIL: " + ex.Message);
        }
    }
    public static void PushCall(string js) => Push(js);
    public static void PushStatus(string text, string type = "") =>
        Push($"window.__setStatus({Json(text)},{Json(type)});");
    public static string ReadFileSafe(string path) { try { return File.ReadAllText(path); } catch { return null; } }

    public static void PushAccounts() => Push($"window.__setAccounts({Json(Vault.All())});");

    internal static void SaveAll() => Vault.Save();

    public void Shutdown()
    {
        Roblox.Shutdown();
        try { Session?.Clear(); } catch { }
    }

    /* ---------- key file / auth ---------- */

    public static void SaveKeyFile(string content, string fileName = "rom-key.json")
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = fileName,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };
        if (dlg.ShowDialog(MainFrame.Inst) == DialogResult.OK)
            File.WriteAllText(dlg.FileName, content);
    }

    /* Вызывается из JS после подтверждения входа */
    public static string PendingKey; // ключ, свежесозданный (для отменённого сохранения)

    public void CompleteLogin(string keyContent, string phrase, string pin) => CompleteAccess(keyContent, phrase, pin);

    public static void Log(string s)
    {
        try
        {
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

    /** Подробный лог только при включённом DevLogMode (не пишет секреты). */
    public static void LogDev(string s)
    {
        try { if (Vault.Settings.DevLogMode) Log(s); } catch { }
    }

    public void CompleteAccess(string keyContent, string phrase, string pin)
    {
        Log("CompleteAccess start: key=" + (keyContent != null ? "yes" : "no"));
        try
        {
            Task.Run(() =>
            {
                try { Log("inner begin"); CompleteAccessInner(keyContent, phrase, pin); Log("inner end"); }
                catch (Exception ex) { Log("inner EX: " + ex); Push("window.openModal(" + Json(Loc.T("Ошибка входа", "Sign-in error")) + "," + Json(Loc.T("Необработанная ошибка: ", "Unexpected error: ") + ex.Message.Replace("'", "")) + ");"); }
            });
        }
        catch (Exception ex)
        {
            Log("outer EX: " + ex);
            Push("window.openModal(" + Json(Loc.T("Ошибка входа", "Sign-in error")) + "," + Json(Loc.T("Необработанная ошибка: ", "Unexpected error: ") + ex.Message.Replace("'", "")) + ");");
        }
    }

    void CompleteAccessInner(string keyContent, string phrase, string pin)
    {
        Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(keyContent))
            { PushStatus(Loc.T("Сначала выберите ключевой файл", "Select a key file first"), "danger"); return; }
            var w = (phrase ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (w.Length != 2) { PushStatus(Loc.T("Фраза должна состоять из двух слов", "The phrase must contain exactly two words"), "danger"); return; }
            if (!Regex.IsMatch(pin ?? "", @"^\d{4,6}$")) { PushStatus(Loc.T("PIN должен быть 4–6 цифр", "PIN must be 4–6 digits"), "danger"); return; }

            var (ok, msg) = Vault.TryUnlock(keyContent, phrase, pin);
            if (!ok)
            {
                Log("TryUnlock fail: " + msg);
                Push("window.openModal(" + Json(Loc.T("Ошибка входа", "Sign-in error")) + "," + Json(msg) + ");");
                return;
            }
            Log("TryUnlock OK");
            Roblox.RestartTimers();
            Roblox.ApplyMultiInstance();
            PushStatus(Loc.T("Вход выполнен", "Signed in"), "success");
            PushAccounts();
            Push("window.__enterDashboard();");
        });
    }

    public void CreateKeyFile(string phrase, string pin, string pinConfirm)
    {
        Task.Run(() =>
        {
            var w = (phrase ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (w.Length != 2) { PushStatus(Loc.T("Фраза должна состоять из двух слов", "The phrase must contain exactly two words"), "danger"); return; }
            if (!Regex.IsMatch(pin ?? "", @"^\d{4,6}$")) { PushStatus(Loc.T("PIN должен быть 4–6 цифр", "PIN must be 4–6 digits"), "danger"); return; }
            if (pin != pinConfirm) { PushStatus(Loc.T("PIN не совпадает", "PINs do not match"), "danger"); return; }

            var iterations = Crypto.DefaultIterations;
            var salt = Crypto.Salt();
            var (_, check) = Crypto.DeriveKey(salt, phrase, pin, iterations);
            var content = Jsons.S(new
            {
                version = 2,
                alg = "AES-256-GCM",
                kdf = "PBKDF2-SHA256",
                iterations,
                salt,
                check,
                created = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                note = "RoManager key"
            });
            MainFrame.Inst.RunOnUi(() =>
            {
                var saved = false;
                using (var dlg = new SaveFileDialog
                {
                    Filter = "JSON (*.json)|*.json",
                    FileName = "rom-key.json",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                })
                {
                    if (dlg.ShowDialog(MainFrame.Inst) == DialogResult.OK)
                    {
                        File.WriteAllText(dlg.FileName, content);
                        saved = true;
                    }
                }
                PendingKey = content;
                // открываем сейв независимо от того, сохранил ли пользователь файл
                var (ok, msg) = Vault.TryUnlock(content, phrase, pin);
                if (!ok) { PushStatus(msg, "danger"); return; }
                Vault.Save();
                PushStatus(saved
                    ? Loc.T("Ключ создан и сохранён. Сейв инициализирован.", "Key created and saved. Vault initialised.")
                    : Loc.T("Ключ создан, но ФАЙЛ НЕ СКАЧАН — нажмите «Скачать ключевой файл»!", "Key created, but the FILE WAS NOT SAVED — click “Download key file”!"), saved ? "success" : "busy");
                PushAccounts();
                Push("window.__enterDashboard();");
            });
        });
    }

    /* ---------- accounts ---------- */

    public void OpenAddAccount()
    {
        Push("window.__showAddAccountModal();");
    }

    public void AddAccountViaBrowser()
    {
        Task.Run(async () =>
        {
            PushStatus(Loc.T("Открываю браузер Roblox для входа…", "Opening the Roblox browser for sign-in…"), "busy");
            Push("window.__closeAddAccountModal();");
            var (cookie, _, _) = await ChromeLogin.StartAsync(s => PushStatus(s, "busy"));
            if (string.IsNullOrEmpty(cookie))
            { PushStatus(Loc.T("Не удалось получить cookie (возможно, аккаунт не вошёл)", "Could not get the cookie (the account may not be signed in)"), "danger"); return; }
            var v = Roblox.ValidateCookie(cookie);
            if (!v.ok) { PushStatus(Loc.T("Cookie недействителен: ", "Invalid cookie: ") + v.err, "danger"); return; }
            var acc = new AccountInfo
            {
                Name = v.name,
                UserId = v.uid,
                Cookie = cookie,
                DataOk = true,
                LastUpdated = DateTime.Now.ToString("dd.MM.yyyy HH:mm")
            };
            Vault.Upsert(acc);
            Vault.Save();
            PushStatus(Loc.F("Аккаунт {0} добавлен", "Account {0} added", v.name), "success");
            PushAccounts();
        });
    }

    public void AddAccountManual(string cookie)
    {
        Task.Run(() =>
        {
            Push("window.__closeAddAccountModal();");
            cookie = (cookie ?? "").Trim();
            if (cookie.StartsWith("_|WARNING")) { /* окей, дальше чистим */ }
            cookie = Regex.Replace(cookie, @"^(_|\.ROBLOSECURITY=|_ROBLOSECURITY=)", "", RegexOptions.IgnoreCase).Trim();
            if (cookie.Length < 50) { PushStatus(Loc.T("Cookie выглядит некорректно", "The cookie looks invalid"), "danger"); return; }
            var v = Roblox.ValidateCookie(cookie);
            if (!v.ok) { PushStatus(Loc.T("Cookie недействителен: ", "Invalid cookie: ") + v.err, "danger"); return; }
            var acc = new AccountInfo
            {
                Name = v.name,
                UserId = v.uid,
                Cookie = cookie,
                DataOk = true,
                LastUpdated = DateTime.Now.ToString("dd.MM.yyyy HH:mm")
            };
            Vault.Upsert(acc);
            Vault.Save();
            PushStatus(Loc.F("Аккаунт {0} добавлен", "Account {0} added", v.name), "success");
            PushAccounts();
        });
    }

    /* «Обновить данные» — проверяем cookie; если протух, открываем окно входа */
    public void RefreshAccount(long id)
    {
        var acc = Vault.Find(id);
        if (acc == null) { PushStatus(Loc.T("Аккаунт не найден", "Account not found"), "danger"); return; }
        Task.Run(() =>
        {
            PushStatus(Loc.F("Проверка {0}…", "Checking {0}…", acc.Name), "busy");
            var v = Roblox.ValidateCookie(acc.Cookie);
            acc.DataOk = v.ok;
            acc.LastUpdated = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            Vault.Save();
            PushAccounts();
            if (v.ok)
            {
                PushStatus(Loc.F("Данные {0} актуальны", "Data for {0} is up to date", acc.Name), "success");
                return;
            }
            PushStatus(Loc.F("Сессия {0} устарела — откройте вход", "The session for {0} has expired — sign in again", acc.Name), "danger");
            OpenBrowserRefresh(id);
        });
    }

    public void DeleteAccounts(string idsJson)
    {
        try
        {
            var ids = JsonSerializer.Deserialize<long[]>(idsJson);
            Vault.Remove(ids);
            Vault.Save();
            PushStatus(Loc.F("Удалено аккаунтов: {0}", "Accounts deleted: {0}", ids.Length), "success");
            PushAccounts();
        }
        catch (Exception ex) { PushStatus(Loc.T("Ошибка удаления: ", "Delete error: ") + ex.Message, "danger"); }
    }

    /* ---------- manage ---------- */
    public void Manage(string mode, string value, long accId)
    {
        var acc = Vault.Find(accId);
        if (acc == null) { PushStatus(Loc.T("Сначала выберите аккаунт", "Select an account first"), "danger"); return; }
        value = (value ?? "").Trim();
        Task.Run(() =>
        {
            try
            {
                if (mode == "launcher")
                {
                    PushStatus(Loc.F("Запуск лаунчера для {0}…", "Launching the launcher for {0}…", acc.Name), "busy");
                    Roblox.Launch(acc, null, null, null, homeMode: true);
                    acc.LastOnline = DateTime.Now.ToString("dd.MM.yyyy HH:mm"); // «Последний запуск»
                    Roblox.RegisterLaunch(acc);
                    PushStatus(Loc.F("Лаунчер Roblox запущен ({0})", "Roblox launcher started ({0})", acc.Name), "success");
                    return;
                }

                // приватный сервер: code / privateServerLinkCode
                string code = null;
                var m = Regex.Match(value, @"(?:code=|privateServerLinkCode=)([A-Za-z0-9\-_]+)");
                if (m.Success) code = m.Groups[1].Value;

                string pid = null, jobId = null, label = null;

                if (mode == "player")
                {
                    PushStatus(Loc.F("Проверяю игрока {0}…", "Looking up player {0}…", value), "busy");
                    var (uid, err) = Roblox.GetUserId(value);
                    if (uid == 0) { PushStatus(Loc.T("Ошибка: ", "Error: ") + err, "danger"); return; }
                    var (place, job, perr) = Roblox.GetUserPlace(acc.Cookie, uid);
                    if (place == null) { PushStatus(Loc.F("К игроку {0}: {1}", "To player {0}: {1}", value, perr), "danger"); return; }
                    pid = place; jobId = job;
                    label = Roblox.GetPlaceName(pid) ?? ("#" + pid);
                    PushStatus(Loc.F("{0} подключается к игроку {1} ({2})…", "{0} is connecting to player {1} ({2})…", acc.Name, value, label), "busy");
                }
                else if (mode == "game")
                {
                    var pm = Regex.Match(value, @"(?:games/|placeId=|\bplaces/)(\d+)");
                    pid = pm.Success ? pm.Groups[1].Value : (Regex.IsMatch(value, @"^\d+$") ? value : null);
                    if (pid == null) { PushStatus(Loc.T("Не понял ID игры", "Could not recognise the game ID"), "danger"); return; }
                    label = Roblox.GetPlaceName(pid) ?? ("#" + pid);
                    PushStatus(code != null
                        ? Loc.F("{0} входит на приват сервер {1}…", "{0} is joining private server {1}…", acc.Name, label)
                        : Loc.F("{0} входит в игру {1}…", "{0} is joining game {1}…", acc.Name, label), "busy");
                }
                else { PushStatus(Loc.T("Неизвестное действие", "Unknown action"), "danger"); return; }

                var delay = Math.Clamp(Vault.Settings.JoinDelayMs, 0, 30000);
                if (delay > 0) Thread.Sleep(delay);

                Roblox.Launch(acc, pid, code, jobId, homeMode: false);
                acc.LastPlaceId = pid;
                acc.LastJobId = jobId;
                acc.LastGameName = label;
                acc.LastOnline = DateTime.Now.ToString("dd.MM.yyyy HH:mm"); // «Последний запуск»
                Roblox.RegisterLaunch(acc);
                PushStatus(Loc.F("{0}: запуск {1}…", "{0}: launching {1}…", acc.Name, label), "success");
            }
            catch (Exception ex)
            {
                PushStatus(Loc.T("Ошибка запуска: ", "Launch error: ") + ex.Message, "danger");
            }
        });
    }

    /* ---------- settings ---------- */
    public string GetSettings() => Json(Vault.Settings);
    public string GetAccounts() => Json(Vault.All());

    public void SaveSettings(string json)
    {
        try
        {
            var s = JsonSerializer.Deserialize<Vault.UserSettings>(json, Jsons.Opt);
            if (s != null)
            {
                Vault.Settings.PresenceRateSec = Math.Clamp(s.PresenceRateSec, 5, 600);
                Vault.Settings.AutoRecheckMin = Math.Clamp(s.AutoRecheckMin, 1, 1440);
                Vault.Settings.AutoCookieRefresh = s.AutoCookieRefresh;
                Vault.Settings.JoinDelayMs = Math.Clamp(s.JoinDelayMs, 0, 30000);
                Vault.Settings.AutoCloseLast = s.AutoCloseLast;
                Vault.Settings.MultiInstance = s.MultiInstance;
                Vault.Settings.UnlockFps = s.UnlockFps;
                Vault.Settings.MaxFps = Math.Clamp(s.MaxFps, 30, 9999);
                Vault.Settings.SharePresence = s.SharePresence;
                Vault.Settings.UseBundledChrome = s.UseBundledChrome;
                Vault.Settings.DevLogMode = s.DevLogMode;
            }
            Vault.Save();
            Roblox.RestartTimers();
            var multiOk = Roblox.ApplyMultiInstance();
            PushStatus(
                multiOk ? Loc.T("Настройки сохранены", "Settings saved")
                        : Loc.T("Настройки сохранены. Multi Roblox не включился: закройте Roblox и сохраните ещё раз",
                                "Settings saved. Multi Roblox could not start: close Roblox and save again"),
                multiOk ? "success" : "danger");
        }
        catch (Exception ex) { PushStatus(Loc.T("Ошибка настроек: ", "Settings error: ") + ex.Message, "danger"); }
    }

    /* ---------- настройки GLSL-фона ----------
       Хранятся отдельным незашифрованным shader.json: фон нужен ещё до входа,
       когда AccountData.json недоступен. */
    static string ShaderSettingsPath => Path.Combine(AppContext.BaseDirectory, "shader.json");

    public string GetShaderSettings()
    {
        try
        {
            if (File.Exists(ShaderSettingsPath)) return File.ReadAllText(ShaderSettingsPath);
        }
        catch (Exception ex) { Log("GetShaderSettings EX: " + ex.Message); }
        return "{}";
    }

    public void SaveShaderSettings(string json)
    {
        try
        {
            File.WriteAllText(ShaderSettingsPath, json ?? "{}");
            Push($"window.__applyShaderSettings({json});");
        }
        catch (Exception ex) { PushStatus(Loc.T("Ошибка сохранения фона: ", "Background save error: ") + ex.Message, "danger"); }
    }

    /* Экспорт ключевого файла текущей сессии */
    public void ExportKeyFile()
    {
        var c = Vault.GetKeyFileContent() ?? PendingKey;
        if (c == null) { PushStatus(Loc.T("Сессия не открыта", "Session is not unlocked"), "danger"); return; }
        SaveKeyFile(c, "rom-key.json");
        PushStatus(Loc.T("Ключевой файл сохранён", "Key file saved"), "success");
    }
    public void Logout()
    {
        Session?.Clear();
        Push("window.goTo('landing');");
        PushStatus(Loc.T("Сессия закрыта", "Session closed"));
    }
}
