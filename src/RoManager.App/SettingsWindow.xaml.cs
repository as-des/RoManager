using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using RoManager.Core;
using Windows.Graphics;
using Windows.UI;

namespace RoManager.App;

/**
 * Полное окно настроек: три вкладки как в 2.1.0, но на стилях приложения
 * (свои чекбоксы, поля, табы и кнопки вместо системных WinUI-контролов).
 * До авторизации доступны интерфейс и фон; Roblox-разделы показаны,
 * но выключены с пояснением «Доступно после входа».
 */
public sealed partial class SettingsWindow : Window
{
    static readonly Regex Hex = new("^#[0-9a-fA-F]{6}$");
    static readonly SolidColorBrush TabActiveBg = new(Color.FromArgb(0xFF, 0x1A, 0x1D, 0x24));
    static readonly SolidColorBrush TabActiveFg = new(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
    static readonly SolidColorBrush TabIdleFg = new(Color.FromArgb(0xFF, 0xAA, 0xAA, 0xAA));

    bool _unlocked;

    public SettingsWindow()
    {
        InitializeComponent();
        Title = Strings.I["settings.title"];
        if (AppWindow is { } appWindow)
        {
            appWindow.Resize(new SizeInt32(536, 679));
            appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "app.ico"));
        }
        WindowChrome.ApplyDark(this);
        // Настройки принадлежат главному окну: закрываются вместе с ним.
        WindowChrome.SetOwner(this, App.Window);
        SettingsBar.Attach(SettingsScroll);

        LoadShaderSettings();
        LoadSettings();
        ApplyLanguage();
        RefreshLockState();
        SetTab("general");

        // Переключатель языка в самих настройках должен перерисовываться
        // сразу, а не только после повторного открытия окна.
        UiEvents.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => UiEvents.LanguageChanged -= OnLanguageChanged;
    }

    void OnLanguageChanged() => DispatcherQueue.TryEnqueue(ApplyLanguage);

    /* ---------- вкладки ---------- */

    void OnTabGeneral(object sender, RoutedEventArgs e) => SetTab("general");
    void OnTabDeveloper(object sender, RoutedEventArgs e) => SetTab("developer");
    void OnTabMisc(object sender, RoutedEventArgs e) => SetTab("misc");

    void SetTab(string tab)
    {
        GeneralPane.Visibility = tab == "general" ? Visibility.Visible : Visibility.Collapsed;
        DeveloperPane.Visibility = tab == "developer" ? Visibility.Visible : Visibility.Collapsed;
        MiscPane.Visibility = tab == "misc" ? Visibility.Visible : Visibility.Collapsed;

        StyleTab(GeneralTab, tab == "general");
        StyleTab(DeveloperTab, tab == "developer");
        StyleTab(MiscTab, tab == "misc");
    }

    static void StyleTab(Button button, bool active)
    {
        button.Background = active ? TabActiveBg : null;
        button.Foreground = active ? TabActiveFg : TabIdleFg;
    }

    /* ---------- язык ---------- */

    public void ApplyLanguage()
    {
        var s = Strings.I;
        Title = s["settings.title"];

        GeneralTab.Content = s["settings.tabs.general"];
        DeveloperTab.Content = s["settings.tabs.developer"];
        MiscTab.Content = s["settings.tabs.misc"];

        InterfaceTitle.Text = s["settings.interface"].ToUpperInvariant();
        LanguageLabel.Text = s["settings.language"];

        ManagerSessionTitle.Text = s["settings.managerSession"].ToUpperInvariant();
        SaveSessionCheck.Content = s["settings.saveSession"];
        SessionDaysLabel.Text = s["settings.sessionDays"];
        SessionHint.Text = s["settings.sessionHint"];
        ForgetSessionButton.Content = s["settings.forgetSession"];
        UpdateSessionControls();

        BackgroundTitle.Text = s["settings.background"].ToUpperInvariant();
        ShaderEnabledCheck.Content = s["settings.animatedBg"];
        WaveColorsLabel.Text = s["settings.waveColors"];
        Wave1Label.Text = s["settings.wave1"];
        Wave2Label.Text = s["settings.wave2"];
        Wave3Label.Text = s["settings.wave3"];
        SpeedLabel.Text = s["settings.speed"];
        SpeedHint.Text = s.F("settings.speedHint", ShaderConfig.SpeedMin.ToString("0.00"), ShaderConfig.SpeedMax.ToString("0.00"));

        LockBannerText.Text = s["settings.locked"];

        SessionTitle.Text = s["settings.session"].ToUpperInvariant();
        AutoCookieCheck.Content = s["settings.autoCookie"];
        RefreshLabel.Text = s["settings.refreshInterval"];
        DelayLabel.Text = s["settings.launchDelay"];
        ClosePrevCheck.Content = s["settings.closePrev"];

        MultiTitle.Text = s["settings.multiTitle"].ToUpperInvariant();
        MultiCheck.Content = s["settings.multiLabel"];
        MultiHint.Text = s["settings.multiHint"];

        PresenceTitle.Text = s["settings.presence"].ToUpperInvariant();
        PresenceCheck.Content = s["settings.showPresence"];
        PresenceRateLabel.Text = s["settings.updateSec"];

        FpsTitle.Text = "FPS";
        UnlockFpsCheck.Content = s["settings.unlockFps"];
        MaxFpsLabel.Text = s["settings.maxFps"];

        BrowserTitle.Text = s["settings.browser"].ToUpperInvariant();
        BundledCheck.Content = s["settings.bundled"];
        BundledHint.Text = s["settings.bundledHint"];
        AppliedHint.Text = s["settings.appliedHint"];

        DebugTitle.Text = s["settings.debug"].ToUpperInvariant();
        DevLogCheck.Content = s["settings.devLog"];
        DevLogHint.Text = s["settings.devLogHint"];

        AboutTitle.Text = s["settings.about"].ToUpperInvariant();
        AboutDescription.Text = s["settings.aboutDesc"];
        GitHubButtonText.Text = s["settings.github"];
        CheckUpdatesText.Text = s["settings.checkUpdates"];
        VersionLabel.Text = s["settings.version"];
        LicenseLabel.Text = s["settings.license"];
        LicenseValue.Text = "MIT";
        DataFolderLabel.Text = s["settings.dataFolder"];
        VersionValue.Text = AppInfo.Version;
        DataFolderValue.Text = AppContext.BaseDirectory;

        SaveButton.Content = s["common.save"];
        CancelButton.Content = s["common.cancel"];
        SaveStatus.Text = "";

        LangSwitch.ApplyLanguage();
        SetTab(GeneralPane.Visibility == Visibility.Visible ? "general" :
               DeveloperPane.Visibility == Visibility.Visible ? "developer" : "misc");
    }

    /* ---------- «О программе» ---------- */

    const string RepoUrl = AppInfo.RepoUrl;
    const string ReleasesApi = AppInfo.ReleasesApi;

    static readonly HttpClient Http = CreateHttp();

    static HttpClient CreateHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("RoManager/1.0");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }

    async void OnOpenGitHub(object sender, RoutedEventArgs e)
    {
        try { await Windows.System.Launcher.LaunchUriAsync(new Uri(RepoUrl)); }
        catch (Exception ex) { App.Dump("OpenGitHub", ex); }
    }

    async void OnCheckUpdates(object sender, RoutedEventArgs e)
    {
        var s = Strings.I;
        CheckUpdatesButton.IsEnabled = false;
        UpdateStatus.Text = s["settings.updateChecking"];

        try
        {
            using var response = await Http.GetAsync(ReleasesApi);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                UpdateStatus.Text = s["settings.updateNoReleases"];
                return;
            }

            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            string tag = doc.RootElement.TryGetProperty("tag_name", out var value) ? value.GetString() : null;

            var remote = ParseVersion(tag);
            var local = ParseVersion(CurrentVersion());
            if (remote != null && local != null && remote > local)
                UpdateStatus.Text = s.F("settings.updateAvailable", tag);
            else
                UpdateStatus.Text = s["settings.updateUpToDate"];
        }
        catch (Exception ex)
        {
            App.Dump("CheckUpdates", ex);
            UpdateStatus.Text = s["settings.updateFailed"];
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    static string CurrentVersion() => AppInfo.Version;

    /** "v1.2.3-beta" → Version(1,2,3). */
    static Version ParseVersion(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var trimmed = text.Trim().TrimStart('v', 'V');
        int cut = trimmed.IndexOfAny(new[] { '-', '+', ' ' });
        if (cut > 0) trimmed = trimmed[..cut];
        return Version.TryParse(trimmed, out var version) ? version : null;
    }

    /* ---------- состояние доступа ---------- */

    public void RefreshLockState()
    {
        _unlocked = CoreHost.Session?.Ready == true;

        LockBanner.Visibility = _unlocked ? Visibility.Collapsed : Visibility.Visible;

        // Настройки интерфейса и фона доступны всегда; Roblox-разделы — после входа.
        foreach (var section in new UIElement[] { SessionSection, MultiSection, PresenceSection, FpsSection, BrowserSection, DebugSection })
        {
            section.Opacity = _unlocked ? 1 : 0.45;
            section.IsHitTestVisible = _unlocked;
        }
        SaveButton.IsEnabled = true;
    }

    /** Сессия открылась/закрылась, пока окно настроек уже было открыто. */
    public void RefreshSession()
    {
        LoadSettings();
        RefreshLockState();
    }

    /* ---------- фон ---------- */

    void LoadShaderSettings()
    {
        var sh = ShaderConfig.Current;
        ShaderEnabledCheck.IsChecked = sh.Enabled;
        Color1Box.Text = sh.Color1;
        Color2Box.Text = sh.Color2;
        Color3Box.Text = sh.Color3;
        SpeedSlider.Value = sh.Speed;
        SpeedValue.Text = sh.Speed.ToString("0.00") + "×";
        UpdateSwatches();
    }

    void OnColorChanged(object sender, TextChangedEventArgs e) => UpdateSwatches();

    bool _syncingColor;

    void UpdateSwatches()
    {
        Color1SwatchFill.Background = Swatch(Color1Box.Text);
        Color2SwatchFill.Background = Swatch(Color2Box.Text);
        Color3SwatchFill.Background = Swatch(Color3Box.Text);
    }

    static Brush Swatch(string hex) =>
        Hex.IsMatch(hex ?? "") ? new SolidColorBrush(ColorFromHex(hex)) : new SolidColorBrush(Color.FromArgb(0xFF, 0x33, 0x33, 0x33));

    /* ---------- палитра цветов ---------- */

    void OnPickColor1(object sender, RoutedEventArgs e) => ShowPicker(Color1Swatch);
    void OnPickColor2(object sender, RoutedEventArgs e) => ShowPicker(Color2Swatch);
    void OnPickColor3(object sender, RoutedEventArgs e) => ShowPicker(Color3Swatch);

    static void ShowPicker(Button button)
    {
        // Flyout живёт на кнопке; показываем явно, чтобы клик срабатывал
        // независимо от кастомного шаблона кнопки.
        if (button.Flyout is Flyout flyout && !flyout.IsOpen)
            flyout.ShowAt(button);
    }

    void OnColor1FlyoutOpened(object sender, object e) => SyncPicker(Color1Picker, Color1Box);
    void OnColor2FlyoutOpened(object sender, object e) => SyncPicker(Color2Picker, Color2Box);
    void OnColor3FlyoutOpened(object sender, object e) => SyncPicker(Color3Picker, Color3Box);

    void OnColor1PickerChanged(ColorPicker sender, ColorChangedEventArgs args) =>
        PickerChanged(Color1Box, args.NewColor);
    void OnColor2PickerChanged(ColorPicker sender, ColorChangedEventArgs args) =>
        PickerChanged(Color2Box, args.NewColor);
    void OnColor3PickerChanged(ColorPicker sender, ColorChangedEventArgs args) =>
        PickerChanged(Color3Box, args.NewColor);

    void SyncPicker(ColorPicker picker, TextBox box)
    {
        if (!Hex.IsMatch(box.Text ?? "")) return;
        _syncingColor = true;
        picker.Color = ColorFromHex(box.Text);
        _syncingColor = false;
    }

    void PickerChanged(TextBox box, Color color)
    {
        if (_syncingColor) return;
        var hex = $"#{color.R:x2}{color.G:x2}{color.B:x2}";
        if (!string.Equals(box.Text, hex, StringComparison.OrdinalIgnoreCase))
            box.Text = hex;
    }

    void OnSpeedChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (SpeedValue == null) return;
        SpeedValue.Text = SpeedSlider.Value.ToString("0.00") + "×";
    }

    /* ---------- значения ---------- */

    void LoadSettings()
    {
        try
        {
            var session = SessionStore.Current;
            SaveSessionCheck.IsChecked = session.Enabled;
            SessionDaysBox.Text = session.Days.ToString();
            UpdateSessionControls();

            var s = Vault.Settings;
            AutoCookieCheck.IsChecked = s.AutoCookieRefresh;
            RefreshBox.Text = s.AutoRecheckMin.ToString();
            DelayBox.Text = s.JoinDelayMs.ToString();
            ClosePrevCheck.IsChecked = s.AutoCloseLast;
            MultiCheck.IsChecked = s.MultiInstance;
            PresenceCheck.IsChecked = s.SharePresence;
            PresenceRateBox.Text = s.PresenceRateSec.ToString();
            UnlockFpsCheck.IsChecked = s.UnlockFps;
            MaxFpsBox.Text = s.MaxFps.ToString();
            BundledCheck.IsChecked = s.UseBundledChrome;
            DevLogCheck.IsChecked = s.DevLogMode;
        }
        catch (Exception ex)
        {
            App.Dump("LoadSettings", ex);
        }
    }

    static int IntFromText(TextBox box, int fallback)
    {
        var text = (box.Text ?? "").Trim();
        return int.TryParse(text, out var value) ? value : fallback;
    }

    /* ---------- сохранение сессии ---------- */

    void OnSaveSessionToggled(object sender, RoutedEventArgs e) => UpdateSessionControls();

    void UpdateSessionControls()
    {
        if (SessionDaysBox == null || ForgetSessionButton == null) return;
        bool enabled = SaveSessionCheck.IsChecked == true;
        SessionDaysBox.IsEnabled = enabled;
        SessionDaysBox.Opacity = enabled ? 1 : 0.45;
        SessionDaysLabel.Opacity = enabled ? 1 : 0.45;
        ForgetSessionButton.Visibility = SessionStore.HasCredentials
            ? Visibility.Visible : Visibility.Collapsed;
    }

    void OnForgetSession(object sender, RoutedEventArgs e)
    {
        SessionStore.ClearCredentials();
        UpdateSessionControls();
        SaveStatus.Text = Strings.I["status.sessionForgotten"];
        CoreHost.PushStatus(Strings.I["status.sessionForgotten"], "success");
    }

    async void OnSave(object sender, RoutedEventArgs e)
    {
        if (!Hex.IsMatch(Color1Box.Text) || !Hex.IsMatch(Color2Box.Text) || !Hex.IsMatch(Color3Box.Text))
        {
            SaveStatus.Text = Strings.I["settings.badColor"];
            return;
        }

        try
        {
            // «Запоминать сессию» хранится отдельно от хранилища: настройку
            // нужно видеть до входа, когда сейв ещё зашифрован.
            bool remember = SaveSessionCheck.IsChecked == true;
            int days = Math.Clamp(IntFromText(SessionDaysBox, SessionStore.Current.Days),
                SessionStore.MinDays, SessionStore.MaxDays);
            SessionStore.Configure(remember, days);
            if (!remember)
                SessionStore.ClearCredentials();
            else if (_unlocked && CoreHost.Session is { } session)
                SessionStore.SaveSession(Vault.GetKeyFileContent(), session.Phrase, session.Pin,
                    SessionStore.Current.KeyFileName);
            UpdateSessionControls();

            if (_unlocked)
            {
                var s = Vault.Settings;
                s.AutoCookieRefresh = AutoCookieCheck.IsChecked == true;
                s.AutoRecheckMin = Math.Clamp(IntFromText(RefreshBox, s.AutoRecheckMin), 1, 240);
                s.JoinDelayMs = Math.Clamp(IntFromText(DelayBox, s.JoinDelayMs), 0, 60000);
                s.AutoCloseLast = ClosePrevCheck.IsChecked == true;
                s.MultiInstance = MultiCheck.IsChecked == true;
                s.SharePresence = PresenceCheck.IsChecked == true;
                s.PresenceRateSec = Math.Clamp(IntFromText(PresenceRateBox, s.PresenceRateSec), 10, 600);
                s.UnlockFps = UnlockFpsCheck.IsChecked == true;
                s.MaxFps = Math.Clamp(IntFromText(MaxFpsBox, s.MaxFps), 30, 1000);
                s.UseBundledChrome = BundledCheck.IsChecked == true;
                s.DevLogMode = DevLogCheck.IsChecked == true;

                Vault.Save();
                await Task.Run(() => Roblox.ApplyMultiInstance());
            }

            // Фон хранится отдельно (shader.json) и доступен до входа.
            ShaderConfig.Save(new ShaderSettings
            {
                Enabled = ShaderEnabledCheck.IsChecked == true,
                Color1 = Color1Box.Text,
                Color2 = Color2Box.Text,
                Color3 = Color3Box.Text,
                Speed = SpeedSlider.Value,
                // Рендерер задаётся вручную в shader.json — не сбрасываем его.
                Renderer = ShaderConfig.Current.Renderer,
            });

            SaveStatus.Text = Strings.I["status.settingsSaved"];
            CoreHost.PushStatus(Strings.I["status.settingsSaved"], "success");
            UiEvents.RaiseSettingsSaved();

            // Как в 2.1.0: окно закрывается через 600 мс после сохранения.
            var timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            timer.Interval = TimeSpan.FromMilliseconds(600);
            timer.Tick += (_, _) => { timer.Stop(); Close(); };
            timer.Start();
        }
        catch (Exception ex)
        {
            App.Dump("SaveSettings", ex);
            SaveStatus.Text = Strings.I["err.unexpected"] + ex.Message;
            CoreHost.PushStatus(Strings.I["err.unexpected"] + ex.Message, "danger");
        }
    }

    void OnCancel(object sender, RoutedEventArgs e) => Close();

    static Color ColorFromHex(string hex) => Color.FromArgb(
        0xFF,
        Convert.ToByte(hex.Substring(1, 2), 16),
        Convert.ToByte(hex.Substring(3, 2), 16),
        Convert.ToByte(hex.Substring(5, 2), 16));
}
