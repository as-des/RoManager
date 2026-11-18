using System.Text.RegularExpressions;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using RoManager.App.Controls;
using RoManager.Core;
using Windows.Foundation;
using Windows.System;
using Windows.UI;

namespace RoManager.App.Views;

/**
 * Дашборд как в 2.1.0: таблица аккаунтов и правая колонка
 * «Действия» → «Управление аккаунтом». Настройки открываются
 * единой кнопкой в шапке.
 */
public sealed partial class DashboardView : UserControl
{
    readonly HashSet<long> _selected = new();
    int _lastIndex = -1;
    string _manageMode; // game | player
    bool _dialogOpen;

    public event Action LogoutRequested;
    public event Action SettingsRequested;

    static readonly Color Accent = Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6);
    static readonly SolidColorBrush RowDivider = new(Color.FromArgb(0xFF, 0x1A, 0x1A, 0x1A));
    static readonly SolidColorBrush RowHover = new(Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF));
    static readonly SolidColorBrush MutedBrush = new(Color.FromArgb(0xFF, 0x88, 0x88, 0x88));
    static readonly SolidColorBrush TextBrush = new(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
    static readonly SolidColorBrush SubtleBrush = new(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
    static readonly SolidColorBrush CheckBorder = new(Color.FromArgb(0xFF, 0x44, 0x44, 0x44));

    public DashboardView()
    {
        InitializeComponent();
        ApplyLanguage();
        Loaded += (_, _) =>
        {
            ApplyResponsiveLayout();
            AccountsBar.Attach(AccountsScroll);
        };
        SizeChanged += (_, _) => ApplyResponsiveLayout();
        ConfigureSegmentHover();
    }

    void ConfigureSegmentHover()
    {
        var segments = new[] { (GameSegment, "game"), (PlayerSegment, "player"), (LauncherSegment, "launcher") };
        foreach (var (segment, mode) in segments)
        {
            segment.PointerEntered += (_, _) =>
            {
                if (_selected.Count != 1 || _manageMode == mode) return;
                segment.Background = SegmentBgHover;
                segment.BorderBrush = SegmentBorderHover;
            };
            segment.PointerExited += (_, _) =>
            {
                if (_manageMode == mode) return;
                segment.Background = SegmentBg;
                segment.BorderBrush = SegmentBorder;
            };
        }
    }

    public void ApplyLanguage()
    {
        var s = Strings.I;
        AccountsTitle.Text = s["dash.accounts"];
        ColAccount.Text = s["dash.colAccount"];
        ColStatus.Text = s["dash.colStatus"];
        ColLast.Text = s["dash.colLast"];
        ColData.Text = s["dash.colData"];
        ActionsTitle.Text = s["dash.actions"];
        AddText.Text = s["dash.add"];
        DeleteButton.Tag = s["dash.delete"];
        RefreshButton.Tag = s["dash.refresh"];
        ManageTitle.Text = s["dash.manage"];
        LauncherLabel.Text = s["dash.launchLauncher"];
        GameInput.PlaceholderText = s["dash.serverId"];
        PlayerInput.PlaceholderText = s["dash.playerNick"];
        LogoutButton.Content = s["dash.logout"];

        ToolTipService.SetToolTip(RefreshButton, s["dash.refresh"]);
        ToolTipService.SetToolTip(DeleteButton, s["dash.delete"]);
        ToolTipService.SetToolTip(DashboardSettingsButton, s["landing.settings"]);
        Reload();
    }

    /* ---------- таблица ---------- */

    public void Reload()
    {
        // Подтягиваем актуальный список и подчищаем выделение удалённых.
        List<AccountInfo> accounts;
        try { accounts = Vault.All()?.ToList() ?? new List<AccountInfo>(); }
        catch (Exception ex)
        {
            App.Dump("DashboardLoad", ex);
            accounts = new List<AccountInfo>();
        }

        var alive = accounts.Select(a => a.Id).ToHashSet();
        _selected.RemoveWhere(id => !alive.Contains(id));
        if (_selected.Count != 1) _manageMode = null;

        AccountList.Children.Clear();
        AccountsTitle.Text = $"{Strings.I["dash.accounts"]} ({accounts.Count})";

        if (accounts.Count == 0)
        {
            AccountList.Children.Add(new TextBlock
            {
                Text = Strings.I["dash.empty"],
                Style = (Style)Application.Current.Resources["AppMutedText"],
                Margin = new Thickness(12, 18, 12, 12),
            });
        }
        else
        {
            for (int i = 0; i < accounts.Count; i++)
                AccountList.Children.Add(BuildRow(accounts[i], i));
        }

        AccountList.Children.Add(BuildSpacer());
        UpdateControls(accounts);
    }

    static FrameworkElement BuildSpacer() => new Border { Height = 8 };

    FrameworkElement BuildRow(AccountInfo account, int index)
    {
        bool selected = _selected.Contains(account.Id);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25, GridUnitType.Star) });

        // Чекбокс
        var box = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1.5),
            BorderBrush = selected ? new SolidColorBrush(Accent) : CheckBorder,
            Background = selected ? new SolidColorBrush(Accent) : new SolidColorBrush(Colors.Transparent),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        if (selected)
        {
            box.Child = new FontIcon
            {
                Glyph = "\uE73E",
                FontSize = 10,
                Foreground = new SolidColorBrush(Colors.White),
            };
        }
        Grid.SetColumn(box, 0);
        grid.Children.Add(box);

        var name = new TextBlock
        {
            Text = string.IsNullOrEmpty(account.Name) ? "—" : account.Name,
            Foreground = TextBrush,
            FontSize = 13.5,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 10, 0),
        };
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);

        var status = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        };
        status.Children.Add(new Ellipse
        {
            Width = 7,
            Height = 7,
            Fill = (Brush)Application.Current.Resources[account.Online ? "AppSuccess" : "AppMuted"],
            VerticalAlignment = VerticalAlignment.Center,
        });
        status.Children.Add(new TextBlock
        {
            Text = Strings.I[account.Online ? "dash.online" : "dash.offline"],
            Foreground = TextBrush,
            FontSize = 13.5,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(status, 2);
        grid.Children.Add(status);

        var last = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(account.LastOnline) ? "—" : account.LastOnline,
            Foreground = MutedBrush,
            FontSize = 13.5,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 10, 0),
        };
        Grid.SetColumn(last, 3);
        grid.Children.Add(last);

        var data = new TextBlock
        {
            Text = Strings.I[account.DataOk ? "dash.dataOk" : "dash.dataOld"],
            Foreground = (Brush)Application.Current.Resources[account.DataOk ? "AppSuccess" : "AppWarning"],
            FontSize = 13.5,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(data, 4);
        grid.Children.Add(data);

        var row = new Border
        {
            Padding = new Thickness(12, 13, 10, 13),
            BorderBrush = RowDivider,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = selected ? SelectionBrush() : new SolidColorBrush(Colors.Transparent),
            Child = grid,
            Tag = account.Id,
        };
        row.Tapped += (s, e) => OnRowTapped(account.Id, index);
        row.PointerEntered += (s, e) => { if (!_selected.Contains(account.Id)) row.Background = RowHover; };
        row.PointerExited += (s, e) => { if (!_selected.Contains(account.Id)) row.Background = new SolidColorBrush(Colors.Transparent); };
        return row;
    }

    static LinearGradientBrush SelectionBrush() => new()
    {
        StartPoint = new Point(0, 0.5),
        EndPoint = new Point(1, 0.5),
        GradientStops =
        {
            new GradientStop { Color = Color.FromArgb(0x00, 0x3B, 0x82, 0xF6), Offset = 0 },
            new GradientStop { Color = Color.FromArgb(0x33, 0x3B, 0x82, 0xF6), Offset = 0.15 },
            new GradientStop { Color = Color.FromArgb(0x33, 0x3B, 0x82, 0xF6), Offset = 0.85 },
            new GradientStop { Color = Color.FromArgb(0x00, 0x3B, 0x82, 0xF6), Offset = 1 },
        },
    };

    void OnRowTapped(long id, int index)
    {
        bool additive = IsCtrlDown();
        bool range = IsShiftDown();

        if (additive)
        {
            if (!_selected.Remove(id)) _selected.Add(id);
            _lastIndex = index;
        }
        else if (range && _lastIndex >= 0)
        {
            var accounts = Vault.All();
            int start = Math.Min(_lastIndex, index);
            int end = Math.Max(_lastIndex, index);
            _selected.Clear();
            for (int i = start; i <= end && i < accounts.Count; i++) _selected.Add(accounts[i].Id);
        }
        else if (_selected.Count == 1 && _selected.Contains(id))
        {
            _selected.Clear();
            _lastIndex = -1;
        }
        else
        {
            _selected.Clear();
            _selected.Add(id);
            _lastIndex = index;
        }

        if (_selected.Count != 1) _manageMode = null;
        UpdateStatusText();
        Reload();
    }

    static bool IsCtrlDown() =>
        (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) &
         Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

    static bool IsShiftDown() =>
        (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) &
         Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

    void UpdateStatusText()
    {
        if (_selected.Count == 0)
        {
            CoreHost.PushStatus(Strings.I["dash.ready"], "idle");
            return;
        }
        if (_selected.Count == 1)
        {
            var acc = Vault.Find(_selected.First());
            CoreHost.PushStatus(Strings.I.F("dash.selectedOne", acc?.Name ?? "—"), "idle");
            return;
        }
        CoreHost.PushStatus(Strings.I.F("dash.selectedMany", _selected.Count), "idle");
    }

    static readonly SolidColorBrush SegmentBg = new(Color.FromArgb(0x80, 0x1A, 0x1C, 0x24));
    static readonly SolidColorBrush SegmentBorder = new(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
    static readonly SolidColorBrush SegmentBgActive = new(Color.FromArgb(0x99, 0x0F, 0x11, 0x18));
    static readonly SolidColorBrush SegmentBorderActive = new(Accent);
    static readonly SolidColorBrush SegmentBgHover = new(Color.FromArgb(0xA6, 0x28, 0x2B, 0x34));
    static readonly SolidColorBrush SegmentBorderHover = new(Color.FromArgb(0x29, 0xFF, 0xFF, 0xFF));
    static readonly SolidColorBrush SegmentIconIdle = new(Color.FromArgb(0xFF, 0x88, 0x88, 0x88));

    /* ---------- адаптивная сетка (как media queries 2.1.0) ---------- */

    void ApplyResponsiveLayout()
    {
        double w = ActualWidth;
        if (w >= 1100)
        {
            DashBody.Padding = new Thickness(28, 24, 28, 24);
            DashBody.ColumnSpacing = 20;
            DashHeader.Padding = new Thickness(28, 16, 28, 16);
            ColSidebar.Width = new GridLength(320);
            Sidebar.SetValue(Grid.RowProperty, 0);
            Sidebar.SetValue(Grid.ColumnProperty, 1);
            RowAccounts.Height = new GridLength(1, GridUnitType.Star);
            RowSidebar.Height = GridLength.Auto;
            AccountsPanel.Height = double.NaN;
        }
        else if (w >= 900)
        {
            DashBody.Padding = new Thickness(20, 20, 20, 20);
            DashBody.ColumnSpacing = 16;
            DashHeader.Padding = new Thickness(20, 13, 20, 13);
            ColSidebar.Width = new GridLength(280);
            Sidebar.SetValue(Grid.RowProperty, 0);
            Sidebar.SetValue(Grid.ColumnProperty, 1);
            RowAccounts.Height = new GridLength(1, GridUnitType.Star);
            RowSidebar.Height = GridLength.Auto;
            AccountsPanel.Height = double.NaN;
        }
        else
        {
            DashBody.Padding = new Thickness(18, 18, 18, 18);
            DashBody.ColumnSpacing = 0;
            DashHeader.Padding = new Thickness(14, 10, 14, 10);
            ColSidebar.Width = new GridLength(0);
            Sidebar.SetValue(Grid.RowProperty, 1);
            Sidebar.SetValue(Grid.ColumnProperty, 0);
            RowAccounts.Height = GridLength.Auto;
            RowSidebar.Height = GridLength.Auto;
            AccountsPanel.Height = Math.Clamp(ActualHeight * 0.52, 280, 560);
        }
    }

    void UpdateControls(List<AccountInfo> accounts)
    {
        RefreshButton.IsEnabled = _selected.Count == 1;
        DeleteButton.IsEnabled = _selected.Count > 0;

        bool canManage = _selected.Count == 1;
        if (!canManage) _manageMode = null;

        // В legacy гасились только дети панели, а не сама панель.
        ManageInner.Opacity = canManage ? 1 : 0.4;
        ManageInner.IsHitTestVisible = canManage;
        ManageTitle.Foreground = canManage
            ? new SolidColorBrush(Color.FromArgb(0xFF, 0xCC, 0xCC, 0xCC))
            : new SolidColorBrush(Color.FromArgb(0xFF, 0x55, 0x55, 0x55));

        ApplyManageLayout();
    }

    void ApplyManageLayout()
    {
        bool has = _manageMode != null;
        bool game = _manageMode == "game";
        bool player = _manageMode == "player";
        bool launcher = _manageMode == "launcher";

        ManageColGame.Width = has && !game ? new GridLength(44) : new GridLength(1, GridUnitType.Star);
        ManageColPlayer.Width = has && !player ? new GridLength(44) : new GridLength(1, GridUnitType.Star);
        ManageColLauncher.Width = has && !launcher ? new GridLength(44) : new GridLength(1, GridUnitType.Star);

        // В раскрытом сегменте иконка убирается целиком: остаются поле + кнопка
        // (у лаунчера — кнопка запуска + подпись).
        GameIcon.Visibility = game ? Visibility.Collapsed : Visibility.Visible;
        PlayerIcon.Visibility = player ? Visibility.Collapsed : Visibility.Visible;
        LauncherIcon.Visibility = launcher ? Visibility.Collapsed : Visibility.Visible;

        GameContent.Visibility = game ? Visibility.Visible : Visibility.Collapsed;
        PlayerContent.Visibility = player ? Visibility.Visible : Visibility.Collapsed;
        LauncherContent.Visibility = launcher ? Visibility.Visible : Visibility.Collapsed;

        SegmentBgTo(GameSegment, game);
        SegmentBgTo(PlayerSegment, player);
        SegmentBgTo(LauncherSegment, launcher);
        GameIcon.Foreground = game ? SegmentBorderActive : SegmentIconIdle;
        PlayerIcon.Foreground = player ? SegmentBorderActive : SegmentIconIdle;
        LauncherIcon.Foreground = launcher ? SegmentBorderActive : SegmentIconIdle;
    }

    static void SegmentBgTo(Border segment, bool active)
    {
        segment.Background = active ? SegmentBgActive : SegmentBg;
        segment.BorderBrush = active ? SegmentBorderActive : SegmentBorder;
    }

    /* ---------- действия ---------- */

    void OnGameSegmentTapped(object sender, TappedRoutedEventArgs e) =>
        ToggleManageMode("game");

    void OnPlayerSegmentTapped(object sender, TappedRoutedEventArgs e) =>
        ToggleManageMode("player");

    void OnLauncherSegmentTapped(object sender, TappedRoutedEventArgs e)
    {
        if (_selected.Count != 1) return;
        if (_manageMode == "launcher") LaunchSelected();
        else
        {
            _manageMode = "launcher";
            ApplyManageLayout();
        }
    }

    void OnManageContentTapped(object sender, TappedRoutedEventArgs e) => e.Handled = true;

    void ToggleManageMode(string mode)
    {
        if (_selected.Count != 1) return;
        _manageMode = _manageMode == mode ? null : mode;
        ApplyManageLayout();
        if (_manageMode == "game") GameInput.Focus(FocusState.Programmatic);
        else if (_manageMode == "player") PlayerInput.Focus(FocusState.Programmatic);
    }

    void OnManageKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            if (_manageMode == "game" || _manageMode == "player") ExecuteManage(_manageMode);
        }
    }

    void OnGameSubmit(object sender, RoutedEventArgs e) => ExecuteManage("game");
    void OnPlayerSubmit(object sender, RoutedEventArgs e) => ExecuteManage("player");

    void ExecuteManage(string mode)
    {
        if (_selected.Count != 1 || _manageMode != mode) return;
        var input = mode == "player" ? PlayerInput : GameInput;
        var value = (input.Text ?? "").Trim();
        if (value.Length == 0) return;

        var acc = Vault.Find(_selected.First());
        if (acc == null) return;
        input.Text = "";
        _manageMode = null;
        ApplyManageLayout();
        RunManage(mode, value, acc);
    }

    async void RunManage(string mode, string value, AccountInfo acc)
    {
        try
        {
            Status(Strings.I.F(mode == "game" ? "dash.enteringGame" : "dash.joiningPlayer", acc.Name, value), "busy");

            await Task.Run(() =>
            {
                string code = null;
                var m = Regex.Match(value, @"(?:code=|privateServerLinkCode=)([A-Za-z0-9\-_]+)");
                if (m.Success) code = m.Groups[1].Value;

                string pid = null, jobId = null, label = null;

                if (mode == "player")
                {
                    var (uid, err) = Roblox.GetUserId(value);
                    if (uid == 0) { Status(Strings.I["err.unexpected"] + err, "danger"); return; }
                    var (place, job, perr) = Roblox.GetUserPlace(acc.Cookie, uid);
                    if (place == null) { Status(Strings.I.F("dash.noPlayerPlace", value, perr), "danger"); return; }
                    pid = place;
                    jobId = job;
                    label = Roblox.GetPlaceName(pid) ?? ("#" + pid);
                }
                else
                {
                    var pm = Regex.Match(value, @"(?:games/|placeId=|\bplaces/)(\d+)");
                    pid = pm.Success ? pm.Groups[1].Value : (Regex.IsMatch(value, @"^\d+$") ? value : null);
                    if (pid == null) { Status(Strings.I["dash.badGameId"], "danger"); return; }
                    label = Roblox.GetPlaceName(pid) ?? ("#" + pid);
                }

                var delay = Math.Clamp(Vault.Settings.JoinDelayMs, 0, 30000);
                if (delay > 0) Thread.Sleep(delay);

                Roblox.Launch(acc, pid, code, jobId, homeMode: false);
                acc.LastPlaceId = pid;
                acc.LastJobId = jobId;
                acc.LastGameName = label;
                acc.LastOnline = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
                Roblox.RegisterLaunch(acc);
                Status(Strings.I.F("dash.launching", acc.Name, label), "success");
            });
        }
        catch (Exception ex)
        {
            Status(Strings.I["dash.launchError"] + ex.Message, "danger");
        }
    }

    void OnLaunch(object sender, RoutedEventArgs e) => LaunchSelected();

    async void LaunchSelected()
    {
        if (_selected.Count != 1) return;
        var acc = Vault.Find(_selected.First());
        if (acc == null) return;

        _manageMode = null;
        ApplyManageLayout();
        try
        {
            Status(Strings.I.F("dash.launchingLauncher", acc.Name), "busy");
            await Task.Run(() =>
            {
                Roblox.Launch(acc, null, null, null, homeMode: true);
                acc.LastOnline = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
                Roblox.RegisterLaunch(acc);
            });
            Status(Strings.I.F("dash.launcherStarted", acc.Name), "success");
        }
        catch (Exception ex)
        {
            Status(Strings.I["dash.launchError"] + ex.Message, "danger");
        }
    }

    async void OnRefreshAccount(object sender, RoutedEventArgs e)
    {
        if (_selected.Count != 1) return;
        var acc = Vault.Find(_selected.First());
        if (acc == null) return;

        Status(Strings.I.F("dash.checking", acc.Name), "busy");
        var v = await Task.Run(() => Roblox.ValidateCookie(acc.Cookie));
        acc.DataOk = v.ok;
        acc.LastUpdated = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        Vault.Save();
        Reload();
        Status(v.ok
            ? Strings.I.F("dash.dataFresh", acc.Name)
            : Strings.I.F("dash.dataExpired", acc.Name),
            v.ok ? "success" : "danger");
    }

    /* ---------- добавление аккаунта ---------- */

    void OnAddAccount(object sender, RoutedEventArgs e)
    {
        if (_dialogOpen) return;
        _dialogOpen = true;
        var s = Strings.I;

        var panel = new StackPanel();

        panel.Children.Add(new TextBlock
        {
            Text = s["add.title"],
            Foreground = (Brush)Application.Current.Resources["AppText"],
            FontSize = 17,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6),
        });
        panel.Children.Add(new TextBlock
        {
            Text = s["add.hint"],
            Style = (Style)Application.Current.Resources["AppMutedText"],
            Margin = new Thickness(0, 0, 0, 14),
        });

        var browser = new Button
        {
            Style = (Style)Application.Current.Resources["AppPrimaryButton"],
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon { Glyph = "\uE774", FontSize = 15 },
                    new TextBlock { Text = s["add.browser"], VerticalAlignment = VerticalAlignment.Center },
                },
            },
        };
        browser.Click += (_, _) =>
        {
            CloseModal();
            _ = AddViaBrowser();
        };
        panel.Children.Add(browser);

        var gap1 = new Border { Height = 10 };
        panel.Children.Add(gap1);

        var cookie = new PasswordBox
        {
            Style = (Style)Application.Current.Resources["AppPasswordBox"],
            PlaceholderText = s["add.cookie"],
        };
        panel.Children.Add(cookie);

        panel.Children.Add(new Border { Height = 8 });

        var byCookie = new Button
        {
            Style = (Style)Application.Current.Resources["AppSecondaryButton"],
            Content = s["add.byCookie"],
        };
        byCookie.Click += (_, _) =>
        {
            var value = cookie.Password;
            CloseModal();
            _ = AddByCookie(value);
        };
        panel.Children.Add(byCookie);

        var cancel = new Button
        {
            Style = (Style)Application.Current.Resources["AppGhostButton"],
            Content = s["common.cancel"],
            Margin = new Thickness(0, 10, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(12, 10, 12, 10),
            FontSize = 14,
        };
        cancel.Click += (_, _) => CloseModal();
        panel.Children.Add(cancel);

        ShowModal(panel, 440);
    }

    void ShowModal(UIElement content, double width = 380)
    {
        ModalCard.Width = width;
        ModalBody.Children.Clear();
        ModalBody.Children.Add(content);
        ModalOverlay.Visibility = Visibility.Visible;
    }

    void CloseModal()
    {
        ModalOverlay.Visibility = Visibility.Collapsed;
        ModalBody.Children.Clear();
        _dialogOpen = false;
    }

    async Task AddViaBrowser()
    {
        try
        {
            Status(Strings.I["add.openingBrowser"], "busy");
            var (cookie, _, _) = await ChromeLogin.StartAsync(t => Status(t, "busy"));
            if (string.IsNullOrEmpty(cookie))
            {
                Status(Strings.I["add.noCookie"], "danger");
                return;
            }
            await StoreCookie(cookie);
        }
        catch (Exception ex)
        {
            Status(Strings.I["err.unexpected"] + ex.Message, "danger");
        }
    }

    async Task AddByCookie(string raw)
    {
        var cookie = (raw ?? "").Trim();
        cookie = Regex.Replace(cookie, @"^(_|\.ROBLOSECURITY=|_ROBLOSECURITY=)", "", RegexOptions.IgnoreCase).Trim();
        if (cookie.Length < 50)
        {
            Status(Strings.I["add.badCookie"], "danger");
            return;
        }
        await StoreCookie(cookie);
    }

    async Task StoreCookie(string cookie)
    {
        var v = await Task.Run(() => Roblox.ValidateCookie(cookie));
        if (!v.ok)
        {
            Status(Strings.I["add.invalidCookie"] + v.err, "danger");
            return;
        }

        var account = new AccountInfo
        {
            Name = v.name,
            UserId = v.uid,
            Cookie = cookie,
            DataOk = true,
            LastUpdated = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
        };
        Vault.Upsert(account);
        Vault.Save();
        Reload();
        Status(Strings.I.F("add.added", v.name), "success");
    }

    /* ---------- удаление ---------- */

    void OnDeleteAccounts(object sender, RoutedEventArgs e)
    {
        if (_selected.Count == 0 || _dialogOpen) return;
        _dialogOpen = true;

        var s = Strings.I;
        var names = _selected.Select(id => Vault.Find(id)?.Name ?? "—").ToList();
        var ids = _selected.ToArray();
        string text = names.Count == 1
            ? s.F("dash.deleteOne", names[0])
            : s.F("dash.deleteMany", string.Join(", ", names));

        var panel = new StackPanel();

        panel.Children.Add(new TextBlock
        {
            Text = s["dash.deleteHeading"],
            Foreground = (Brush)Application.Current.Resources["AppText"],
            FontSize = 17,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 16),
        });
        panel.Children.Add(new TextBlock
        {
            Text = text,
            Style = (Style)Application.Current.Resources["AppMutedText"],
            Margin = new Thickness(0, 0, 0, 18),
        });

        var confirm = new Button
        {
            Style = (Style)Application.Current.Resources["AppDangerButton"],
            Content = s["dash.delete"],
        };
        confirm.Click += (_, _) =>
        {
            CloseModal();
            try
            {
                Vault.Remove(ids);
                Vault.Save();
                _selected.Clear();
                _manageMode = null;
                Reload();
                Status(s.F("dash.deleted", ids.Length), "success");
            }
            catch (Exception ex)
            {
                App.Dump("DeleteAccounts", ex);
            }
        };
        panel.Children.Add(confirm);

        var cancel = new Button
        {
            Style = (Style)Application.Current.Resources["AppSecondaryButton"],
            Content = s["common.cancel"],
            Margin = new Thickness(0, 10, 0, 0),
        };
        cancel.Click += (_, _) => CloseModal();
        panel.Children.Add(cancel);

        ShowModal(panel);
    }

    /* ---------- прочее ---------- */

    void OnOpenSettings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();
    void OnLogout(object sender, RoutedEventArgs e) => LogoutRequested?.Invoke();

    static void Status(string text, string kind) => CoreHost.PushStatus(text, kind);
}
