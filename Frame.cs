using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace RoManager;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainFrame());
    }
}

internal sealed class MainFrame : Form
{
    public static MainFrame Inst;
    WebView2 _wv;
    public Bridge Bridge = new();

    public MainFrame()
    {
        Inst = this;
        Text = "RoManager";
        // Нативная рамка Windows: заголовок, кнопки и ресайз рисует система.
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1000, 680);
        BackColor = Color.FromArgb(26, 26, 26);
        MinimumSize = new Size(760, 520);
        try { Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "app.ico")); } catch { }

        // чистим старые временные профили браузера от прошлых запусков
        Task.Run(() =>
        {
            try
            {
                foreach (var d in Directory.GetDirectories(Path.GetTempPath(), "rom_prof_*"))
                    try { Directory.Delete(d, true); } catch { }
            }
            catch { }
        });

        // Тёмный фон до первой отрисовки — чтобы при старте и ресайзе не мигало белым.
        _wv = new WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = Color.FromArgb(10, 10, 10),
        };
        Controls.Add(_wv);

        Load += async (_, _) =>
        {
            var env = await CoreWebView2Environment.CreateAsync(
                userDataFolder: Path.Combine(AppContext.BaseDirectory, "wv2"));
            await _wv.EnsureCoreWebView2Async(env);
            var wv2 = _wv.CoreWebView2;
            wv2.Settings.AreDefaultContextMenusEnabled = false;
            wv2.Settings.IsZoomControlEnabled = false;
            wv2.Settings.IsStatusBarEnabled = false;
            wv2.Settings.IsPasswordAutosaveEnabled = false;
            wv2.Settings.IsGeneralAutofillEnabled = false;
#if DEBUG
            wv2.Settings.AreDevToolsEnabled = true;
#else
            wv2.Settings.AreDevToolsEnabled = false;
            wv2.Settings.AreBrowserAcceleratorKeysEnabled = false;
#endif
            /* Никакой навигации вне локального UI: мост доступен только app.local. */
            wv2.NavigationStarting += (_, e) =>
            {
                if (!e.Uri.StartsWith("https://app.local/", StringComparison.OrdinalIgnoreCase))
                    e.Cancel = true;
            };
            wv2.NewWindowRequested += (_, e) => e.Handled = true;
            wv2.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            Bridge.Attach(wv2);
            // Пока пользователь тянет рамку, UI отключает тяжёлые эффекты (блюр/шейдер),
            // чтобы окно меняло размер без подвисаний.
            ResizeBegin += (_, _) =>
                Bridge.Push("window.__setResizing && window.__setResizing(true);");
            ResizeEnd += (_, _) =>
                Bridge.Push("window.__setResizing && window.__setResizing(false);");
            wv2.WebMessageReceived += (_, e) =>
            { if (e.TryGetWebMessageAsString() == "drag") DragWindow(); };
            MapLocal(wv2);
#if DEBUG
            // ROM_TEST_UI=v2/index.html — проверить другой UI, не трогая production.
            var uiPath = Environment.GetEnvironmentVariable("ROM_TEST_UI");
            if (string.IsNullOrWhiteSpace(uiPath)) uiPath = "index.html";
#else
            var uiPath = "index.html";
#endif
            wv2.Navigate("https://app.local/" + uiPath.TrimStart('/'));

            /* Тестовый хук (без SendKeys/кликов по чужим окнам):
               переменные окружения ROM_TEST_KEY / ROM_TEST_PHRASE / ROM_TEST_PIN
               и необязательное ROM_TEST_ACTION=launcher|game:ID|player:NICK|addbrowser|refresh
               Собирается только в Debug — в релизе этих хуков нет. */
#if DEBUG
            var testKey = Environment.GetEnvironmentVariable("ROM_TEST_KEY");
            var testJsEnv = Environment.GetEnvironmentVariable("ROM_TEST_JS");
            if (!string.IsNullOrEmpty(testKey) || !string.IsNullOrEmpty(testJsEnv))
            {
                bool ran = false;
                wv2.NavigationCompleted += async (_, _) =>
                {
                    if (ran) return;
                    ran = true;
                    if (!string.IsNullOrEmpty(testKey) && File.Exists(testKey))
                        Bridge.CompleteAccess(File.ReadAllText(testKey).Trim(),
                            Environment.GetEnvironmentVariable("ROM_TEST_PHRASE") ?? "",
                            Environment.GetEnvironmentVariable("ROM_TEST_PIN") ?? "");
                    await Task.Delay(3000);
                    var testJs = Environment.GetEnvironmentVariable("ROM_TEST_JS");
                    if (!string.IsNullOrEmpty(testJs))
                    {
                        try
                        {
                            var jsResult = await wv2.ExecuteScriptAsync(testJs);
                            Bridge.Log("TEST js result: " + jsResult);
                        }
                        catch (Exception ex) { Bridge.Log("TEST js EX: " + ex.Message); }
                    }
                    var resizeTest = Environment.GetEnvironmentVariable("ROM_TEST_RESIZE");
                    if (!string.IsNullOrEmpty(resizeTest))
                    {
                        var parts = resizeTest.Split(',');
                        if (parts.Length == 2 &&
                            int.TryParse(parts[0], out var rdx) &&
                            int.TryParse(parts[1], out var rdy))
                        {
                            Bridge.ResizeBy(rdx, rdy);
                            Bridge.Log($"TEST resize {rdx},{rdy} -> {MainFrame.Inst.Bounds}");
                        }
                    }
                    var act = Environment.GetEnvironmentVariable("ROM_TEST_ACTION") ?? "";
                    if (string.IsNullOrEmpty(act)) return;
                    if (act == "addbrowser") { Bridge.OpenBrowserLogin(); return; }
                    if (act == "addwith")
                    {
                        try
                        {
                            var env3 = await CoreWebView2Environment.CreateAsync(
                                userDataFolder: Path.Combine(AppContext.BaseDirectory, "wv2"));
                            var tmp3 = new Form { ShowInTaskbar = false, Size = new Size(400, 300), StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000) };
                            var w3 = new WebView2 { Dock = DockStyle.Fill };
                            tmp3.Controls.Add(w3); tmp3.Show();
                            await w3.EnsureCoreWebView2Async(env3);
                            var d3 = new TaskCompletionSource<bool>();
                            w3.CoreWebView2.NavigationCompleted += (_, _) => d3.TrySetResult(true);
                            w3.CoreWebView2.Navigate("https://www.roblox.com/home");
                            await Task.WhenAny(d3.Task, Task.Delay(20000));
                            var cs3 = await w3.CoreWebView2.CookieManager.GetCookiesAsync("https://www.roblox.com/");
                            var cc3 = cs3.FirstOrDefault(x => x.Name == ".ROBLOSECURITY");
                            Bridge.Log("TEST addwith cookie len=" + (cc3?.Value?.Length ?? 0));
                            w3.Dispose(); tmp3.Close();
                            if (cc3 != null && cc3.Value.Length > 50)
                                MainFrame.Inst.RunOnUi(() =>
                                {
                                    var f3 = new BrowserLoginFrame(injectCookie: cc3.Value);
                                    f3.Show(MainFrame.Inst);
                                });
                        }
                        catch (Exception ex) { Bridge.Log("TEST addwith EX: " + ex.Message); }
                        return;
                    }
                    if (act == "ticketwv")
                    {
                        try
                        {
                            var env2 = await CoreWebView2Environment.CreateAsync(
                                userDataFolder: Path.Combine(AppContext.BaseDirectory, "wv2"));
                            var tmp = new Form { ShowInTaskbar = false, Size = new Size(400, 300), StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000) };
                            var w2 = new WebView2 { Dock = DockStyle.Fill };
                            tmp.Controls.Add(w2); tmp.Show();
                            await w2.EnsureCoreWebView2Async(env2);
                            var done = new TaskCompletionSource<bool>();
                            w2.CoreWebView2.NavigationCompleted += (_, _) => done.TrySetResult(true);
                            w2.CoreWebView2.Navigate("https://www.roblox.com/home");
                            await Task.WhenAny(done.Task, Task.Delay(20000));
                            var cs = await w2.CoreWebView2.CookieManager.GetCookiesAsync("https://www.roblox.com/");
                            var cc = cs.FirstOrDefault(x => x.Name == ".ROBLOSECURITY");
                            Bridge.Log("TEST wv cookie len=" + (cc?.Value?.Length ?? 0));
                            if (cc != null && cc.Value.Length > 50)
                            {
                                foreach (var ep in new[] { "https://auth.roblox.com/v1/authentication-ticket" })
                                {
                                    try
                                    {
                                        var probe = Roblox.AuthReq(ep, cc.Value);
                                        var pr = await Roblox.Http.SendAsync(probe);
                                        string csrf = pr.Headers.TryGetValues("x-csrf-token", out var pv) ? pv.FirstOrDefault() : null;

                                        var req = Roblox.AuthReq(ep, cc.Value);
                                        if (csrf != null) req.Headers.TryAddWithoutValidation("x-csrf-token", csrf);
                                        var rr = await Roblox.Http.SendAsync(req);
                                        var rtxt = await rr.Content.ReadAsStringAsync();
                                        string tk = rr.Headers.TryGetValues("rbx-authentication-ticket", out var tv) ? tv.FirstOrDefault() : null;
                                        Bridge.Log($"TEST ep={ep} probe={(int)pr.StatusCode} final={(int)rr.StatusCode} ticket={(tk != null ? "OK len=" + tk.Length : "none")} body={rtxt.Truncate(120)}");
                                    }
                                    catch (Exception ex) { Bridge.Log("TEST ep=" + ep + " EX " + ex.Message); }
                                }
                            }
                            w2.Dispose(); tmp.Close();
                        }
                        catch (Exception ex) { Bridge.Log("TEST ticketwv EX: " + ex.Message); }
                        return;
                    }
                    var acc = Vault.All().FirstOrDefault();
                    if (acc == null) return;
                    if (act == "refresh") { Bridge.OpenBrowserRefresh(acc.Id); return; }
                    if (act == "settings") { Bridge.OpenSettings(); return; }
                    if (act == "manyfake")
                    {
                        var fake = Enumerable.Range(1, 25).Select(i => new AccountInfo
                        {
                            Id = i,
                            Name = "fake_account_" + i,
                            UserId = i,
                            Online = i % 2 == 0,
                            DataOk = true,
                            LastOnline = i % 3 == 0 ? "29.10.2026 08:15" : "—"
                        }).ToList();
                        Bridge.Push($"window.__setAccounts({Bridge.Json(fake)});");
                        return;
                    }
                    if (act == "settingsclose")
                    {
                        Bridge.OpenSettings();
                        await Task.Delay(4000);
                        Bridge.CloseSettingsWindow();
                        Bridge.Log("TEST settings close requested");
                        return;
                    }
                    if (act == "ticket")
                    {
                        var (tk, te) = Roblox.GetAuthTicket(acc.Cookie);
                        Bridge.Log("TEST ticket: " + (tk != null ? "OK len=" + tk.Length : "FAIL " + te));
                        return;
                    }
                    var p = act.Split(':', 2);
                    Bridge.Manage(p[0], p.Length > 1 ? p[1] : "", acc.Id);
                };
            }
#endif
        };
    }

    void MapLocal(CoreWebView2 wv2)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
        wv2.AddWebResourceRequestedFilter("https://app.local/*", CoreWebView2WebResourceContext.All);
        wv2.WebResourceRequested += (_, e) =>
        {
            var rel = new Uri(e.Request.Uri).AbsolutePath.TrimStart('/');
            var file = Path.GetFullPath(Path.Combine(root,
                Uri.UnescapeDataString(rel).Replace('/', Path.DirectorySeparatorChar)));
            if (file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(file))
            {
                e.Response = wv2.Environment.CreateWebResourceResponse(
                    File.OpenRead(file), 200, "OK", MimeOf(file));
            }
            else
            {
                e.Response = wv2.Environment.CreateWebResourceResponse(
                    null, 404, "Not Found", "Content-Type: text/plain; charset=utf-8\r\n");
            }
        };
    }

    // Без корректных MIME-типов Chromium не грузит ES-модули и стили.
    internal static string MimeOf(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".html" => "Content-Type: text/html; charset=utf-8\r\n",
        ".js" or ".mjs" => "Content-Type: text/javascript; charset=utf-8\r\n",
        ".css" => "Content-Type: text/css; charset=utf-8\r\n",
        ".json" or ".map" => "Content-Type: application/json; charset=utf-8\r\n",
        ".png" => "Content-Type: image/png\r\n",
        ".ico" => "Content-Type: image/x-icon\r\n",
        ".svg" => "Content-Type: image/svg+xml\r\n",
        ".woff2" => "Content-Type: font/woff2\r\n",
        ".woff" => "Content-Type: font/woff\r\n",
        _ => "Content-Type: application/octet-stream\r\n",
    };

    void DragFile(object sender, DragEventArgs e)
    {
        e.Effect = DragDropEffects.Copy;
        if (e.Data.GetFormats().Contains("FileDrop"))
        {
            var files = (string[])e.Data.GetData("FileDrop");
            Bridge.PushCall($"window.__keyFileDropped({Bridge.Json(new
            {
                name = Path.GetFileName(files[0]),
                content = Bridge.ReadFileSafe(files[0])
            })});");
        }
    }

    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
    public void DragWindow()
    {
        ReleaseCapture();
        _ = SendMessage(Handle, 0xA1, 2, 0);
    }
    public void Minimize()
    {
        if (InvokeRequired) { BeginInvoke(Minimize); return; }
        WindowState = FormWindowState.Minimized;
    }
    public void ToggleMax()
    {
        if (InvokeRequired) { BeginInvoke(ToggleMax); return; }
        WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
    }
    public void Quit()
    {
        if (InvokeRequired) { BeginInvoke(Quit); return; }
        Close();
    }
    public void RunOnUi(Action a)
    {
        if (InvokeRequired) BeginInvoke(a); else a();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Тёмный системный заголовок (Windows 10/11), пока окно ещё не показано.
        try
        {
            int v = 1;
            DwmSetWindowAttribute(Handle, 20, ref v, 4);
            DwmSetWindowAttribute(Handle, 19, ref v, 4);
        }
        catch { }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            int v = 1;
            DwmSetWindowAttribute(Handle, 20, ref v, 4);
            DwmSetWindowAttribute(Handle, 19, ref v, 4);
        }
        catch { }
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        Bridge.Shutdown();
        base.OnFormClosing(e);
    }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
}
