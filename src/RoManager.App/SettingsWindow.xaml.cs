using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RoManager.Core;
using Windows.Graphics;

namespace RoManager.App;

/**
 * Полное окно настроек: три вкладки как в 2.1.0.
 * До авторизации доступны только интерфейсные настройки, Roblox-разделы
 * показываются, но выключены с пояснением «Доступно после входа».
 */
public sealed partial class SettingsWindow : Window
{
    bool _languageLoading;
    bool _unlocked;

    public SettingsWindow()
    {
        InitializeComponent();
        Title = Strings.I["settings.title"];
        if (AppWindow is { } appWindow)
        {
            appWindow.Resize(new SizeInt32(600, 760));
            appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "app.ico"));
        }
        WindowChrome.ApplyDark(this);

        BuildLanguageItems();
        LoadSettings();
        ApplyLanguage();
        RefreshLockState();
    }

    /* ---------- язык ---------- */

    void BuildLanguageItems()
    {
        _languageLoading = true;
        LanguageBox.Items.Clear();
        LanguageBox.Items.Add(new ComboBoxItem { Content = "Русский", Tag = "ru" });
        LanguageBox.Items.Add(new ComboBoxItem { Content = "English", Tag = "en" });
        LanguageBox.SelectedIndex = Loc.IsEnglish ? 1 : 0;
        _languageLoading = false;
    }

    void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_languageLoading) return;
        if (LanguageBox.SelectedItem is not ComboBoxItem item || item.Tag is not string lang) return;
        if (lang == Loc.Language) return;

        Loc.Set(lang);
        UiEvents.RaiseLanguageChanged();
        ApplyLanguage();
    }

    public void ApplyLanguage()
    {
        var s = Strings.I;
        Title = s["settings.title"];

        GeneralTab.Header = s["settings.tabs.general"];
        DeveloperTab.Header = s["settings.tabs.developer"];
        MiscTab.Header = s["settings.tabs.misc"];

        InterfaceTitle.Text = s["settings.interface"];
        LanguageLabel.Text = s["settings.language"];
        LockBannerText.Text = s["settings.locked"];

        SessionTitle.Text = s["settings.session"];
        AutoCookieLabel.Text = s["settings.autoCookie"];
        RefreshLabel.Text = s["settings.refreshInterval"];
        DelayLabel.Text = s["settings.launchDelay"];
        ClosePrevLabel.Text = s["settings.closePrev"];

        MultiTitle.Text = s["settings.multiTitle"];
        MultiLabel.Text = s["settings.multiLabel"];
        MultiHint.Text = s["settings.multiHint"];

        PresenceTitle.Text = s["settings.presence"];
        ShowPresenceLabel.Text = s["settings.showPresence"];
        PresenceRateLabel.Text = s["settings.updateSec"];

        FpsTitle.Text = s["settings.presence"] + " · FPS";
        UnlockFpsLabel.Text = s["settings.unlockFps"];
        MaxFpsLabel.Text = s["settings.maxFps"];

        BrowserTitle.Text = s["settings.browser"];
        BundledLabel.Text = s["settings.bundled"];
        BundledHint.Text = s["settings.bundledHint"];
        AppliedHint.Text = s["settings.appliedHint"];

        DebugTitle.Text = s["settings.debug"];
        DevLogLabel.Text = s["settings.devLog"];
        DevLogHint.Text = s["settings.devLogHint"];

        AboutTitle.Text = s["settings.about"];
        VersionLabel.Text = s["settings.version"];
        DataFolderLabel.Text = s["settings.dataFolder"];
        VersionValue.Text = Assembly.GetExecutingAssembly().GetName().Version is { } v
            ? $"{v.Major}.{v.Minor}.{v.Build}"
            : "3.0";
        DataFolderValue.Text = AppContext.BaseDirectory;

        SaveButton.Content = s["common.save"];
        SaveStatus.Text = "";
    }

    /* ---------- состояние доступа ---------- */

    public void RefreshLockState()
    {
        _unlocked = CoreHost.Session?.Ready == true;

        LockBanner.Visibility = _unlocked ? Visibility.Collapsed : Visibility.Visible;
        SessionSection.IsEnabled = _unlocked;
        MultiSection.IsEnabled = _unlocked;
        PresenceSection.IsEnabled = _unlocked;
        FpsSection.IsEnabled = _unlocked;
        BrowserSection.IsEnabled = _unlocked;
        DevLogToggle.IsEnabled = _unlocked;
        SaveButton.IsEnabled = _unlocked;

        // Выключенные разделы дополнительно приглушаем: системный disabled-вид
        // на тёмной теме слишком похож на обычный.
        SessionSection.Opacity = _unlocked ? 1 : 0.45;
        MultiSection.Opacity = _unlocked ? 1 : 0.45;
        PresenceSection.Opacity = _unlocked ? 1 : 0.45;
        FpsSection.Opacity = _unlocked ? 1 : 0.45;
        BrowserSection.Opacity = _unlocked ? 1 : 0.45;
        DevLogToggle.Opacity = _unlocked ? 1 : 0.45;
    }

    /* ---------- значения ---------- */

    /** Сессия открылась/закрылась, пока окно настроек уже было открыто. */
    public void RefreshSession()
    {
        LoadSettings();
        RefreshLockState();
    }

    void LoadSettings()
    {
        try
        {
            var s = Vault.Settings;
            AutoCookieToggle.IsOn = s.AutoCookieRefresh;
            RefreshBox.Value = s.AutoRecheckMin;
            DelayBox.Value = s.JoinDelayMs;
            ClosePrevToggle.IsOn = s.AutoCloseLast;
            MultiToggle.IsOn = s.MultiInstance;
            PresenceToggle.IsOn = s.SharePresence;
            PresenceRateBox.Value = s.PresenceRateSec;
            UnlockFpsToggle.IsOn = s.UnlockFps;
            MaxFpsBox.Value = s.MaxFps;
            BundledToggle.IsOn = s.UseBundledChrome;
            DevLogToggle.IsOn = s.DevLogMode;
        }
        catch (Exception ex)
        {
            App.Dump("LoadSettings", ex);
        }
    }

    static int Int(NumberBox box, int fallback)
    {
        var value = box.Value;
        if (double.IsNaN(value)) return fallback;
        return (int)Math.Round(value);
    }

    async void OnSave(object sender, RoutedEventArgs e)
    {
        if (!_unlocked) return;

        try
        {
            var s = Vault.Settings;
            s.AutoCookieRefresh = AutoCookieToggle.IsOn;
            s.AutoRecheckMin = Math.Clamp(Int(RefreshBox, s.AutoRecheckMin), 1, 240);
            s.JoinDelayMs = Math.Clamp(Int(DelayBox, s.JoinDelayMs), 0, 60000);
            s.AutoCloseLast = ClosePrevToggle.IsOn;
            s.MultiInstance = MultiToggle.IsOn;
            s.SharePresence = PresenceToggle.IsOn;
            s.PresenceRateSec = Math.Clamp(Int(PresenceRateBox, s.PresenceRateSec), 10, 600);
            s.UnlockFps = UnlockFpsToggle.IsOn;
            s.MaxFps = Math.Clamp(Int(MaxFpsBox, s.MaxFps), 30, 1000);
            s.UseBundledChrome = BundledToggle.IsOn;
            s.DevLogMode = DevLogToggle.IsOn;

            Vault.Save();
            await Task.Run(() => Roblox.ApplyMultiInstance());

            SaveStatus.Text = Strings.I["status.settingsSaved"];
            CoreHost.PushStatus(Strings.I["status.settingsSaved"], "success");
            UiEvents.RaiseSettingsSaved();
        }
        catch (Exception ex)
        {
            App.Dump("SaveSettings", ex);
            SaveStatus.Text = Strings.I["err.unexpected"] + ex.Message;
            CoreHost.PushStatus(Strings.I["err.unexpected"] + ex.Message, "danger");
        }
    }
}
