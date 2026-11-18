using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RoManager.App.Controls;
using RoManager.App.Localization;
using RoManager.App.Views;
using RoManager.Core;
using Windows.Graphics;
using Windows.System;
using Windows.UI;

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

        WindowChrome.ApplyDark(this);
        ApplyLanguage();
        ShowLanding();

        // Esc закрывает историю обновлений.
        Root.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Escape && ChangelogOverlay.Visibility == Visibility.Visible)
            {
                CloseChangelog();
                e.Handled = true;
            }
        };

        ChangelogBar.Attach(ChangelogScroll);

        // Анимированный фон: CPU-порт GLSL из 2.1.0 (shader.json).
        Backdrop.Loaded += (_, _) =>
        {
            BackgroundShader.Attach(Backdrop);
            Scrim.Visibility = ShaderConfig.Current.Enabled ? Visibility.Visible : Visibility.Collapsed;
        };
        ShaderConfig.Changed += () => DispatcherQueue.TryEnqueue(() =>
            Scrim.Visibility = ShaderConfig.Current.Enabled ? Visibility.Visible : Visibility.Collapsed);

        // Не жжём CPU, когда окно свёрнуто или неактивно.
        Activated += (_, e) => BackgroundShader.SetActive(
            e.WindowActivationState != WindowActivationState.Deactivated);

        UiEvents.LanguageChanged += () => DispatcherQueue.TryEnqueue(ApplyLanguage);
        CoreHost.Status = (text, kind) => DispatcherQueue.TryEnqueue(() => SetStatus(text, kind));
        CoreHost.AccountsChanged = () => DispatcherQueue.TryEnqueue(() => _dashboard?.Reload());

#if DEBUG
        // Тестовые крючки для снимков UI (как в 2.1.0): ROM_TEST_SCREEN=settings|wizard|create|dashboard
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
        StatusRight.Text = "v" + Version();
        _landing?.ApplyLanguage();
        _wizard?.ApplyLanguage();
        _dashboard?.ApplyLanguage();
        ChangelogTitle.Text = Strings.I["changelog.title"];
        ChangelogBackText.Text = Strings.I["common.back"];
        if (ChangelogOverlay.Visibility == Visibility.Visible) BuildChangelog();
        ToolTipService.SetToolTip(HeaderSettings, Strings.I["landing.settings"]);
        if (StatusText.Text == null || StatusText.Text.Length == 0 || _statusIsIdle)
            SetStatus(Strings.I["dash.ready"], "idle");
    }

    static string Version()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v == null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    /* ---------- навигация ---------- */

    public void ShowLanding()
    {
        _landing ??= new LandingView();
        _landing.StartRequested -= OnStartRequested;
        _landing.StartRequested += OnStartRequested;
        _landing.ApplyLanguage();
        ContentHost.Content = _landing;
        AuthTopBar.Visibility = Visibility.Visible;
        StatusBar.Visibility = Visibility.Collapsed;
    }

    void OnStartRequested(bool haveAccess)
    {
        _wizard ??= new WizardView();
        _wizard.Completed -= OnSignedIn;
        _wizard.Completed += OnSignedIn;
        _wizard.CancelRequested -= ShowLanding;
        _wizard.CancelRequested += ShowLanding;
        _wizard.Restart(haveAccess);
        ContentHost.Content = _wizard;
        AuthTopBar.Visibility = Visibility.Collapsed;
        StatusBar.Visibility = Visibility.Collapsed;
    }

    void OnSignedIn()
    {
        _dashboard ??= new DashboardView();
        _dashboard.LogoutRequested -= OnLogout;
        _dashboard.LogoutRequested += OnLogout;
        _dashboard.SettingsRequested -= OpenSettings;
        _dashboard.SettingsRequested += OpenSettings;
        _dashboard.ApplyLanguage();
        _dashboard.Reload();
        ContentHost.Content = _dashboard;
        AuthTopBar.Visibility = Visibility.Collapsed;
        StatusBar.Visibility = Visibility.Visible;
        SetStatus(Strings.I["dash.ready"], "idle");
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

    /* ---------- история обновлений ---------- */

    void OnOpenChangelog(object sender, RoutedEventArgs e) => OpenChangelog();

    public void OpenChangelog()
    {
        BuildChangelog();
        ChangelogOverlay.Visibility = Visibility.Visible;
    }

    void CloseChangelog() => ChangelogOverlay.Visibility = Visibility.Collapsed;

    void OnCloseChangelog(object sender, RoutedEventArgs e) => CloseChangelog();

    void OnChangelogBackdropTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e) =>
        CloseChangelog();

    void OnChangelogModalTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e) =>
        e.Handled = true;

    /** Содержимое окна истории: версии, даты и пункты на текущем языке. */
    void BuildChangelog()
    {
        ChangelogBody.Children.Clear();
        bool en = Loc.IsEnglish;

        for (int i = 0; i < ChangelogData.Entries.Length; i++)
        {
            var entry = ChangelogData.Entries[i];

            var section = new StackPanel { Spacing = 12 };

            var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            head.Children.Add(new TextBlock
            {
                Text = "v" + entry.Version,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)),
                FontSize = 15.5,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            head.Children.Add(new TextBlock
            {
                Text = en ? entry.DateEn : entry.DateRu,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x8A, 0x92, 0xA2)),
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Bottom,
            });
            section.Children.Add(head);

            var list = new StackPanel { Spacing = 10 };
            foreach (var change in en ? entry.ChangesEn : entry.ChangesRu)
            {
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                row.Children.Add(new TextBlock
                {
                    Text = "—",
                    Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x6B, 0x72, 0x80)),
                    FontSize = 13.5,
                    Margin = new Thickness(0, 0, 8, 0),
                });

                var body = new TextBlock
                {
                    Text = change,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xC2, 0xC8, 0xD4)),
                    FontSize = 13.5,
                    LineHeight = 23,
                    TextWrapping = TextWrapping.Wrap,
                };
                Grid.SetColumn(body, 1);
                row.Children.Add(body);

                list.Children.Add(row);
            }
            section.Children.Add(list);

            if (i == 0)
            {
                ChangelogBody.Children.Add(section);
            }
            else
            {
                ChangelogBody.Children.Add(new Border
                {
                    Margin = new Thickness(0, 26, 0, 0),
                    Padding = new Thickness(0, 22, 0, 0),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF)),
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Child = section,
                });
            }
        }
    }

    /* ---------- статус ---------- */

    bool _statusIsIdle = true;
    DispatcherTimer _statusTimer;

    public void SetStatus(string text, string kind)
    {
        StatusText.Text = text ?? "";
        _statusIsIdle = kind is null or "idle";
        var brush = kind switch
        {
            "success" => (Brush)Application.Current.Resources["AppSuccess"],
            "danger" => (Brush)Application.Current.Resources["AppDanger"],
            "busy" => (Brush)Application.Current.Resources["AppPrimary"],
            _ => new SolidColorBrush(Color.FromArgb(0xFF, 0x55, 0x55, 0x55)),
        };
        StatusDot.Fill = brush;
        StatusText.Foreground = kind is null or "idle"
            ? new SolidColorBrush(Color.FromArgb(0xFF, 0x77, 0x77, 0x77))
            : new SolidColorBrush(Color.FromArgb(0xFF, 0xAA, 0xAA, 0xAA));

        // success/danger дублируются плашкой сверху, как в 2.1.0.
        if (kind is "success" or "danger")
            Notices.Notify(text, kind);

        ScheduleStatusReset(text, kind);
    }

    /** Через время статус возвращается к «Готов к работе» (App.tsx). */
    void ScheduleStatusReset(string text, string kind)
    {
        _statusTimer?.Stop();
        _statusTimer = null;

        string ready = Strings.I["dash.ready"];
        TimeSpan? delay = kind switch
        {
            "success" => TimeSpan.FromMilliseconds(2500),
            "idle" when text != ready => TimeSpan.FromMilliseconds(3000),
            _ => null,
        };
        if (delay == null) return;

        _statusTimer = new DispatcherTimer { Interval = delay.Value };
        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer?.Stop();
            _statusTimer = null;
            SetStatus(ready, "idle");
        };
        _statusTimer.Start();
    }
}
