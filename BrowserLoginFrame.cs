using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace RoManager;

/* Отдельное дочернее окно логина Roblox (встроенный Chromium = WebView2).
   КАЖДОЕ окно получает СВОЙ временный профиль браузера — поэтому предыдущий
   аккаунт не подтягивается. После успешного входа окно закрывается само,
   аккаунт добавляется/обновляется в списке. */
internal sealed class BrowserLoginFrame : Form
{
    public static BrowserLoginFrame Inst;
    WebView2 _wv;
    TableLayoutPanel _root;
    Label _title;
    System.Windows.Forms.Timer _timer;
    CoreWebView2Environment _env;
    string _profileDir;
    readonly string _injectCookie;
    readonly long? _refreshId;
    bool _done;
    bool _ready;

    public BrowserLoginFrame(string injectCookie = null, long? refreshId = null)
    {
        Inst = this;
        _injectCookie = injectCookie;
        _refreshId = refreshId;
        _profileDir = Path.Combine(Path.GetTempPath(), "rom_prof_" + Guid.NewGuid().ToString("N").Substring(0, 12));

        Text = refreshId != null
            ? Loc.T("Обновление данных — RoManager", "Refreshing data — RoManager")
            : Loc.T("Вход Roblox — RoManager", "Roblox sign-in — RoManager");
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1024, 760);
        MinimumSize = new Size(720, 520);
        BackColor = Color.FromArgb(30, 30, 30);
        ShowInTaskbar = false;
        try { Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "app.ico")); } catch { }

        _root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var bar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(45, 45, 45) };
        _title = new Label
        {
            Text = Text,
            ForeColor = Color.FromArgb(224, 224, 224),
            Font = new Font("Segoe UI", 9f),
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0)
        };
        var closeBtn = new Button
        {
            Text = "✕",
            Dock = DockStyle.Right,
            Width = 46,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(204, 204, 204),
            BackColor = Color.FromArgb(45, 45, 45),
            Font = new Font("Segoe UI", 9f),
            TabStop = false
        };
        closeBtn.FlatAppearance.BorderSize = 0;
        closeBtn.Click += (_, _) => Close();
        bar.Controls.Add(_title);
        bar.Controls.Add(closeBtn);
        bar.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) DragForm(); };
        _title.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) DragForm(); };

        _wv = new WebView2 { Dock = DockStyle.Fill };
        _root.Controls.Add(bar, 0, 0);
        _root.Controls.Add(_wv, 0, 1);
        Controls.Add(_root);

        _timer = new System.Windows.Forms.Timer { Interval = 1200 };
        _timer.Tick += async (_, _) => await CheckCookie();

        Load += async (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(_profileDir);
                Bridge.Log("browser profile: " + _profileDir);
                _env = await CoreWebView2Environment.CreateAsync(userDataFolder: _profileDir);
                await _wv.EnsureCoreWebView2Async(_env);
                var wv2 = _wv.CoreWebView2;
                wv2.Settings.AreDefaultContextMenusEnabled = false;
                wv2.Settings.IsStatusBarEnabled = false;
                wv2.NavigationCompleted += async (_, e) => { if (e.IsSuccess) await CheckCookie(); };

                if (!string.IsNullOrEmpty(_injectCookie))
                {
                    var ck = wv2.CookieManager.CreateCookie(".ROBLOSECURITY", _injectCookie.Trim(), ".roblox.com", "/");
                    ck.IsHttpOnly = true;
                    ck.IsSecure = true;
                    wv2.CookieManager.AddOrUpdateCookie(ck);
                    wv2.Navigate("https://www.roblox.com/my/account");
                }
                else wv2.Navigate("https://www.roblox.com/login");

                _ready = true;
                _timer.Start();
            }
            catch (Exception ex) { Bridge.Log("browser init EX: " + ex.Message); }
        };

        FormClosed += (_, _) =>
        {
            _done = true;
            _ready = false;
            try { _timer.Stop(); _timer.Dispose(); } catch { }
            try { _wv.Dispose(); } catch { }
            _env = null;
            Inst = null;
            var dir = _profileDir;
            // WebView2 отпускает файлы не мгновенно — удаляем в фоне с ретраями
            Task.Run(() =>
            {
                for (int i = 0; i < 12; i++)
                {
                    try { if (Directory.Exists(dir)) Directory.Delete(dir, true); return; }
                    catch { Thread.Sleep(700); }
                }
            });
        };
    }

    async Task CheckCookie()
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => _ = CheckCookie())); return; }
        if (_done || !_ready || IsDisposed || _wv == null || _wv.IsDisposed) return;
        try
        {
            var wv2 = _wv.CoreWebView2;
            if (wv2 == null) return;
            var url = wv2.Source ?? "";
            if (!url.Contains("roblox.com")) return;

            var cookies = await wv2.CookieManager.GetCookiesAsync("https://www.roblox.com/");
            var c = cookies.FirstOrDefault(x => x.Name == ".ROBLOSECURITY");
            if (c == null) return;
            var cookie = c.Value ?? ""; // ВАЖНО: копируем строку на UI-потоке (CoreWebView2Cookie нельзя читать из фонового потока)
            if (cookie.Length < 50) return;
            if (_done) return;

            if (_refreshId != null && string.Equals(cookie, _injectCookie, StringComparison.Ordinal))
                return; // пока cookie тот же — ждём обновления сессии

            _done = true;
            _timer.Stop();
            Bridge.Log("browser window: cookie found (" + cookie.Length + " chars)");

            bool ok = false;
            try
            {
                Bridge.Log("browser window: validating cookie");
                if (_refreshId != null)
                    ok = await Task.Run(() => MainFrame.Inst.Bridge.TryRefreshAccountByCookie(_refreshId.Value, cookie));
                else
                    ok = await Task.Run(() => MainFrame.Inst.Bridge.TryAddAccountByCookie(cookie));
                Bridge.Log("browser window: add result " + ok);
            }
            catch (Exception ex) { Bridge.Log("browser window add EX: " + ex); }

            if (ok) { Bridge.Log("browser window: closing"); Close(); return; }
            _done = false;
            _timer.Start();
        }
        catch (Exception ex)
        {
            if (!IsDisposed) Bridge.Log("browser window cookie: " + ex.Message);
        }
    }

    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
    void DragForm()
    {
        ReleaseCapture();
        _ = SendMessage(Handle, 0xA1, 2, 0);
    }

    /** Заголовок окна входа на текущем языке. */
    public void ApplyLanguage()
    {
        Text = _refreshId != null
            ? Loc.T("Обновление данных — RoManager", "Refreshing data — RoManager")
            : Loc.T("Вход Roblox — RoManager", "Roblox sign-in — RoManager");
        if (_title != null) _title.Text = Text;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try { int v = 1; DwmSetWindowAttribute(Handle, 20, ref v, 4); DwmSetWindowAttribute(Handle, 19, ref v, 4); } catch { }
    }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
}
