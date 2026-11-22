using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RoManager.App.Localization;
using RoManager.Core;
using Windows.Graphics;

namespace RoManager.App;

/**
 * Окно входа в Roblox на встроенном Chromium (WebView2).
 *
 * Внешний браузер для этого не годится: он не отдаёт cookie менеджеру, если
 * открыт без отладочного порта (а Edge/Chrome всё чаще его игнорируют).
 * WebView2 работает внутри процесса, поэтому после входа мы просто читаем
 * .ROBLOSECURITY через CookieManager — так же, как это делала версия 2.1.0.
 *
 * Перед показом страницы cookie прошлых сессий очищаются, поэтому чужой
 * аккаунт не подтягивается.
 */
public sealed partial class BrowserLoginWindow : Window
{
    readonly TaskCompletionSource<string> _result =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    DispatcherTimer _timer;
    bool _ready;
    bool _finished;

    public BrowserLoginWindow()
    {
        InitializeComponent();
        Title = Strings.I["login.title"];
        LoadingText.Text = Strings.I["login.loading"];

        WindowChrome.ApplyDark(this);
        WindowChrome.SetOwner(this, App.Window);
        WindowChrome.SetMinSize(this, 720, 520);
        try { AppWindow.Resize(new SizeInt32(1080, 780)); } catch { }

        Closed += OnClosed;
    }

    /** Показать окно и дождаться cookie; null — окно закрыли без входа. */
    public Task<string> ShowAsync()
    {
        Activate();
        return _result.Task;
    }

    async void OnBrowserLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await Browser.EnsureCoreWebView2Async();

            var core = Browser.CoreWebView2;

            // Чистим cookie прошлых сессий: чужой аккаунт не подтянется.
            try { core.CookieManager.DeleteAllCookies(); } catch { }

            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDevToolsEnabled = false;

            // Всплывающие окна (вход через Google и т.п.) открываем в этом же окне.
            core.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                core.Navigate(args.Uri);
            };
            core.NavigationCompleted += (_, _) =>
            {
                if (LoadingOverlay != null) LoadingOverlay.Visibility = Visibility.Collapsed;
            };

            core.Navigate("https://www.roblox.com/login");
            _ready = true;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
            _timer.Tick += async (_, _) => await CheckCookieAsync();
            _timer.Start();
        }
        catch (Exception ex)
        {
            CoreHost.Log("webview login init fail: " + ex.Message);
            LoadingText.Text = Strings.I["login.failed"];
            _result.TrySetResult(null);
        }
    }

    async Task CheckCookieAsync()
    {
        if (_finished || !_ready) return;
        try
        {
            var source = Browser.Source?.AbsoluteUri ?? "";
            if (!source.Contains("roblox.com")) return;

            var core = Browser.CoreWebView2;
            if (core == null) return;

            var cookies = await core.CookieManager.GetCookiesAsync("https://www.roblox.com/");
            var cookie = cookies.FirstOrDefault(c => c.Name == ".ROBLOSECURITY")?.Value;
            if (string.IsNullOrEmpty(cookie) || cookie.Length < 50) return;

            _finished = true;
            try { _timer?.Stop(); } catch { }
            CoreHost.Log("webview cookie captured (" + cookie.Length + ")");
            _result.TrySetResult(cookie);
            Close();
        }
        catch (Exception ex)
        {
            CoreHost.Log("webview cookie poll: " + ex.Message);
        }
    }

    void OnClosed(object sender, WindowEventArgs args)
    {
        _finished = true;
        try { _timer?.Stop(); } catch { }
        _result.TrySetResult(null);
    }
}
