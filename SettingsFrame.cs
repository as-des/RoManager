using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace RoManager;

/* Отдельное перемещаемое окно настроек (безбордерное, тёмное, свой WebView2) */
internal sealed class SettingsFrame : Form
{
    public static SettingsFrame Inst;
    WebView2 _wv;

    public SettingsFrame()
    {
        Inst = this;
        Text = "Настройки — RoManager";
        // Нативная рамка, как у главного окна: системный заголовок и ресайз.
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 640);
        MinimumSize = new Size(480, 520);
        BackColor = Color.FromArgb(10, 10, 10);
        DoubleBuffered = true;
        ShowInTaskbar = false;
        try { Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "app.ico")); } catch { }

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
            wv2.NavigationStarting += (_, e) =>
            {
                if (!e.Uri.StartsWith("https://app.local/", StringComparison.OrdinalIgnoreCase))
                    e.Cancel = true;
            };
            wv2.NewWindowRequested += (_, e) => e.Handled = true;
            wv2.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            wv2.AddHostObjectToScript("rom", MainFrame.Inst.Bridge);
            MapLocal(wv2);
#if DEBUG
            var testJs = Environment.GetEnvironmentVariable("ROM_TEST_SETTINGS_JS");
            if (!string.IsNullOrEmpty(testJs))
            {
                bool ran = false;
                wv2.NavigationCompleted += async (_, _) =>
                {
                    if (ran) return;
                    ran = true;
                    await Task.Delay(1500);
                    try { await wv2.ExecuteScriptAsync(testJs); }
                    catch (Exception ex) { Bridge.Log("SETTINGS js EX: " + ex.Message); }
                };
            }
#endif
            wv2.Navigate("https://app.local/settings.html");
        };

        FormClosed += (_, _) => Inst = null;
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
                    File.OpenRead(file), 200, "OK", MainFrame.MimeOf(file));
            }
            else
            {
                e.Response = wv2.Environment.CreateWebResourceResponse(
                    null, 404, "Not Found", "Content-Type: text/plain; charset=utf-8\r\n");
            }
        };
    }

    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
    public void DragForm()
    {
        ReleaseCapture();
        _ = SendMessage(Handle, 0xA1, 2, 0);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { int v = 1; DwmSetWindowAttribute(Handle, 20, ref v, 4); DwmSetWindowAttribute(Handle, 19, ref v, 4); } catch { }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try { int v = 1; DwmSetWindowAttribute(Handle, 20, ref v, 4); DwmSetWindowAttribute(Handle, 19, ref v, 4); } catch { }
    }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
}
