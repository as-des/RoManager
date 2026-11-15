using System.Reflection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using RoManager.App.Views;
using RoManager.Core;
using Windows.Graphics;

namespace RoManager.App;

public sealed partial class MainWindow : Window
{
    LandingView _landing;
    WizardView _wizard;
    DashboardView _dashboard;
    SettingsWindow _settings;

    public static IntPtr Handle { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        Title = "RoManager";
        Handle = WinRT.Interop.WindowNative.GetWindowHandle(this);

        if (AppWindow is { } appWindow)
        {
            appWindow.Resize(new SizeInt32(1000, 680));
            appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "app.ico"));
        }

        TryApplyDarkTitleBar();
        ApplyLanguage();
        ShowLanding();

        UiEvents.LanguageChanged += () => DispatcherQueue.TryEnqueue(ApplyLanguage);
        CoreHost.Status = (text, kind) => DispatcherQueue.TryEnqueue(() => SetStatus(text, kind));

#if DEBUG
        // Тестовые крючки для снимков UI (как в 2.1.0): ROM_TEST_SCREEN=settings|wizard
        var testScreen = Environment.GetEnvironmentVariable("ROM_TEST_SCREEN");
        if (!string.IsNullOrEmpty(testScreen))
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (testScreen == "settings") OpenSettings();
                else if (testScreen == "wizard") OnStartRequested(true);
                else if (testScreen == "create") OnStartRequested(false);
            });
        }

        // ROM_TEST_UNLOCK_KEY=<путь к key.json> + ROM_TEST_UNLOCK_PHRASE/PIN:
        // открыть хранилище без ручного ввода (только Debug, значения не логируются).
        var testKey = Environment.GetEnvironmentVariable("ROM_TEST_UNLOCK_KEY");
        if (!string.IsNullOrEmpty(testKey))
        {
            DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    var json = await File.ReadAllTextAsync(testKey);
                    var phrase = Environment.GetEnvironmentVariable("ROM_TEST_UNLOCK_PHRASE");
                    var pin = Environment.GetEnvironmentVariable("ROM_TEST_UNLOCK_PIN");
                    var (ok, msg) = await Task.Run(() => Vault.TryUnlock(json, phrase, pin));
                    if (ok) OnSignedIn();
                    else SetStatus(msg, "danger");
                }
                catch (Exception ex)
                {
                    App.Dump("TestUnlock", ex);
                }
            });
        }
#endif
    }

    public void ApplyLanguage()
    {
        Title = Loc.T("RoManager", "RoManager");
        HeaderSettingsText.Text = Strings.I["landing.settings"];
        StatusRight.Text = "v" + Version();
        _landing?.ApplyLanguage();
        _wizard?.ApplyLanguage();
        _dashboard?.ApplyLanguage();
        if (StatusText.Text == null || StatusText.Text.Length == 0 || _statusIsIdle)
            SetStatus(Strings.I["dash.ready"], "idle");
    }

    static string Version()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v == null ? "3.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    /* ---------- навигация ---------- */

    public void ShowLanding()
    {
        _landing ??= new LandingView();
        _landing.StartRequested -= OnStartRequested;
        _landing.StartRequested += OnStartRequested;
        _landing.ApplyLanguage();
        ContentHost.Content = _landing;
    }

    void OnStartRequested(bool haveAccess)
    {
        _wizard ??= new WizardView();
        _wizard.Completed -= OnSignedIn;
        _wizard.Completed += OnSignedIn;
        _wizard.Restart(haveAccess);
        ContentHost.Content = _wizard;
    }

    void OnSignedIn()
    {
        _dashboard ??= new DashboardView();
        _dashboard.LogoutRequested -= OnLogout;
        _dashboard.LogoutRequested += OnLogout;
        _dashboard.ApplyLanguage();
        _dashboard.Reload();
        ContentHost.Content = _dashboard;
        SetStatus(Strings.I["status.signedIn"], "success");
        _settings?.RefreshSession();
    }

    void OnLogout()
    {
        try { CoreHost.Session?.Clear(); } catch { }
        ShowLanding();
        SetStatus(Strings.I["status.sessionClosed"], "idle");
        _settings?.RefreshSession();
    }

    void OnOpenSettings(object sender, RoutedEventArgs e) => OpenSettings();

    public void OpenSettings()
    {
        if (_settings != null)
        {
            _settings.Activate();
            return;
        }
        _settings = new SettingsWindow();
        _settings.Closed += (_, _) =>
        {
            _settings = null;
            // Настройки могли изменить сессию (например, Multi Roblox) — обновим состояние.
            ApplyLanguage();
        };
        _settings.Activate();
    }

    /* ---------- статус ---------- */

    bool _statusIsIdle = true;

    public void SetStatus(string text, string kind)
    {
        StatusText.Text = text ?? "";
        _statusIsIdle = kind == "idle";
        var brush = kind switch
        {
            "success" => (Brush)Application.Current.Resources["AppSuccess"],
            "danger" => (Brush)Application.Current.Resources["AppDanger"],
            "busy" => (Brush)Application.Current.Resources["AppWarning"],
            _ => (Brush)Application.Current.Resources["AppMuted"],
        };
        StatusDot.Fill = brush;
    }

    /* ---------- окно ---------- */

    void TryApplyDarkTitleBar() => WindowChrome.ApplyDark(this);
}
