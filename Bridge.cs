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

    /* ---------- встроенный логин Roblox (как в оригинале RAM) ---------- */
    public void OpenBrowserLogin()
    {
        if (Session == null || !Session.Ready) { PushStatus("Сначала войдите в приложение", "danger"); return; }
        Log("OpenBrowserLogin");
        PushStatus("Открываю окно входа Roblox…", "busy");
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
        if (!v.ok) { Log("cookie invalid: " + v.err); PushStatus("Cookie недействителен: " + v.err, "danger"); return false; }
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
        PushStatus($"Аккаунт {v.name} добавлен", "success");
        PushAccounts();
        Push("window.__enterDashboard();");
        return true;
    }

    /* Открыть окно «Обновить данные»: профиль тоже свежий, cookie подставляется в него */
    public void OpenBrowserRefresh(long id)
    {
        if (Session == null || !Session.Ready) { PushStatus("Сначала войдите в приложение", "danger"); return; }
        var acc = Vault.Find(id);
        if (acc == null) { PushStatus("Аккаунт не найден", "danger"); return; }
        Log("OpenBrowserRefresh " + acc.Name);
        PushStatus($"Обновление данных {acc.Name}…", "busy");
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
        if (!v.ok) { Log("refresh invalid: " + v.err); PushStatus("Cookie недействителен: " + v.err, "danger"); return false; }
        if (v.uid != acc.UserId) { PushStatus("Вошёл другой аккаунт — обновление отменено", "danger"); return false; }
        acc.Cookie = cookie;
        acc.Name = v.name;
        acc.DataOk = true;
        acc.Online = false;
        acc.LastUpdated = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        Vault.Save();
        Log("account refreshed: " + v.name);
        PushStatus($"Данные {v.name} обновлены", "success");
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
                catch (Exception ex) { Log("inner EX: " + ex); Push("window.openModal('Ошибка входа','Необработанное: " + ex.Message.Replace("'", "") + "');"); }
            });
        }
        catch (Exception ex)
        {
            Log("outer EX: " + ex);
            Push("window.openModal('Ошибка входа','Необработанное: " + ex.Message.Replace("'", "") + "');");
        }
    }

    void CompleteAccessInner(string keyContent, string phrase, string pin)
    {
        Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(keyContent))
            { PushStatus("Сначала выберите ключевой файл", "danger"); return; }
            var w = (phrase ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (w.Length != 2) { PushStatus("Фраза должна состоять из двух слов", "danger"); return; }
            if (!Regex.IsMatch(pin ?? "", @"^\d{4,6}$")) { PushStatus("PIN должен быть 4–6 цифр", "danger"); return; }

            var (ok, msg) = Vault.TryUnlock(keyContent, phrase, pin);
            if (!ok)
            {
                Log("TryUnlock fail: " + msg);
                Push("window.openModal(" + Json("Ошибка входа") + "," + Json(msg) + ");");
                return;
            }
            Log("TryUnlock OK");
            Roblox.RestartTimers();
            Roblox.ApplyMultiInstance();
            PushStatus("Вход выполнен", "success");
            PushAccounts();
            Push("window.__enterDashboard();");
        });
    }

    public void CreateKeyFile(string phrase, string pin, string pinConfirm)
    {
        Task.Run(() =>
        {
            var w = (phrase ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (w.Length != 2) { PushStatus("Фраза должна состоять из двух слов", "danger"); return; }
            if (!Regex.IsMatch(pin ?? "", @"^\d{4,6}$")) { PushStatus("PIN должен быть 4–6 цифр", "danger"); return; }
            if (pin != pinConfirm) { PushStatus("PIN не совпадает", "danger"); return; }

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
                    ? "Ключ создан и сохранён. Сейв инициализирован."
                    : "Ключ создан, но ФАЙЛ НЕ СКАЧАН — нажмите «Скачать ключевой файл»!", saved ? "success" : "busy");
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
            PushStatus("Открываю браузер Roblox для входа…", "busy");
            Push("window.__closeAddAccountModal();");
            var (cookie, _, _) = await ChromeLogin.StartAsync(s => PushStatus(s, "busy"));
            if (string.IsNullOrEmpty(cookie))
            { PushStatus("Не удалось получить cookie (возможно, аккаунт не вошёл)", "danger"); return; }
            var v = Roblox.ValidateCookie(cookie);
            if (!v.ok) { PushStatus("Cookie недействителен: " + v.err, "danger"); return; }
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
            PushStatus($"Аккаунт {v.name} добавлен", "success");
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
            if (cookie.Length < 50) { PushStatus("Cookie выглядит некорректно", "danger"); return; }
            var v = Roblox.ValidateCookie(cookie);
            if (!v.ok) { PushStatus("Cookie недействителен: " + v.err, "danger"); return; }
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
            PushStatus($"Аккаунт {v.name} добавлен", "success");
            PushAccounts();
        });
    }

    /* «Обновить данные» — проверяем cookie; если протух, открываем окно входа */
    public void RefreshAccount(long id)
    {
        var acc = Vault.Find(id);
        if (acc == null) { PushStatus("Аккаунт не найден", "danger"); return; }
        Task.Run(() =>
        {
            PushStatus($"Проверка {acc.Name}…", "busy");
            var v = Roblox.ValidateCookie(acc.Cookie);
            acc.DataOk = v.ok;
            acc.LastUpdated = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            Vault.Save();
            PushAccounts();
            if (v.ok)
            {
                PushStatus($"Данные {acc.Name} актуальны", "success");
                return;
            }
            PushStatus($"Сессия {acc.Name} устарела — откройте вход", "danger");
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
            PushStatus($"Удалено аккаунтов: {ids.Length}", "success");
            PushAccounts();
        }
        catch (Exception ex) { PushStatus("Ошибка удаления: " + ex.Message, "danger"); }
    }

    /* ---------- manage ---------- */
    public void Manage(string mode, string value, long accId)
    {
        var acc = Vault.Find(accId);
        if (acc == null) { PushStatus("Сначала выберите аккаунт", "danger"); return; }
        value = (value ?? "").Trim();
        Task.Run(() =>
        {
            try
            {
                if (mode == "launcher")
                {
                    PushStatus($"Запуск лаунчера для {acc.Name}…", "busy");
                    Roblox.Launch(acc, null, null, null, homeMode: true);
                    acc.LastOnline = DateTime.Now.ToString("dd.MM.yyyy HH:mm"); // «Последний запуск»
                    Roblox.RegisterLaunch(acc);
                    PushStatus($"Лаунчер Roblox запущен ({acc.Name})", "success");
                    return;
                }

                // приватный сервер: code / privateServerLinkCode
                string code = null;
                var m = Regex.Match(value, @"(?:code=|privateServerLinkCode=)([A-Za-z0-9\-_]+)");
                if (m.Success) code = m.Groups[1].Value;

                string pid = null, jobId = null, label = null;

                if (mode == "player")
                {
                    PushStatus($"Проверяю игрока {value}…", "busy");
                    var (uid, err) = Roblox.GetUserId(value);
                    if (uid == 0) { PushStatus("Ошибка: " + err, "danger"); return; }
                    var (place, job, perr) = Roblox.GetUserPlace(acc.Cookie, uid);
                    if (place == null) { PushStatus($"К игроку {value}: {perr}", "danger"); return; }
                    pid = place; jobId = job;
                    label = Roblox.GetPlaceName(pid) ?? ("#" + pid);
                    PushStatus($"{acc.Name} подключается к игроку {value} ({(label)})…", "busy");
                }
                else if (mode == "game")
                {
                    var pm = Regex.Match(value, @"(?:games/|placeId=|\bplaces/)(\d+)");
                    pid = pm.Success ? pm.Groups[1].Value : (Regex.IsMatch(value, @"^\d+$") ? value : null);
                    if (pid == null) { PushStatus("Не понял ID игры", "danger"); return; }
                    label = Roblox.GetPlaceName(pid) ?? ("#" + pid);
                    PushStatus(code != null
                        ? $"{acc.Name} входит на приват сервер {label}…"
                        : $"{acc.Name} входит в игру {label}…", "busy");
                }
                else { PushStatus("Неизвестное действие", "danger"); return; }

                var delay = Math.Clamp(Vault.Settings.JoinDelayMs, 0, 30000);
                if (delay > 0) Thread.Sleep(delay);

                Roblox.Launch(acc, pid, code, jobId, homeMode: false);
                acc.LastPlaceId = pid;
                acc.LastJobId = jobId;
                acc.LastGameName = label;
                acc.LastOnline = DateTime.Now.ToString("dd.MM.yyyy HH:mm"); // «Последний запуск»
                Roblox.RegisterLaunch(acc);
                PushStatus($"{acc.Name}: запуск {label}…", "success");
            }
            catch (Exception ex)
            {
                PushStatus("Ошибка запуска: " + ex.Message, "danger");
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
                multiOk ? "Настройки сохранены"
                        : "Настройки сохранены. Multi Roblox не включился: закройте Roblox и сохраните ещё раз",
                multiOk ? "success" : "danger");
        }
        catch (Exception ex) { PushStatus("Ошибка настроек: " + ex.Message, "danger"); }
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
        catch (Exception ex) { PushStatus("Ошибка сохранения фона: " + ex.Message, "danger"); }
    }

    /* Экспорт ключевого файла текущей сессии */
    public void ExportKeyFile()
    {
        var c = Vault.GetKeyFileContent() ?? PendingKey;
        if (c == null) { PushStatus("Сессия не открыта", "danger"); return; }
        SaveKeyFile(c, "rom-key.json");
        PushStatus("Ключевой файл сохранён", "success");
    }
    public void Logout()
    {
        Session?.Clear();
        Push("window.goTo('landing');");
        PushStatus("Сессия закрыта");
    }
}
