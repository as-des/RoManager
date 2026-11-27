using System.Text.RegularExpressions;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using RoManager.App.Controls;
using RoManager.Core;
using Windows.Foundation;
using Windows.System;
using Windows.UI;

namespace RoManager.App.Views;

/**
 * Dashboard as in 2.1.0: the account table and a right-hand
 * "Actions" -> "Account controls" column. Settings open from a
 * single button in the header.
 */
public sealed partial class DashboardView : UserControl
{
    readonly HashSet<long> _selected = new();
    int _lastIndex = -1;
    string _manageMode; // game | player
    bool _dialogOpen;
    bool _animateManageCollapse;
    AddAccountWindow _addWindow;

    /** Accounts in the current display order (search, favourites, sorting). */
    readonly List<AccountInfo> _view = new();
    string _query = "";
    string _sortColumn = "";
    bool _sortDesc;

    public event Action LogoutRequested;
    public event Action SettingsRequested;

    /** Accent comes from the theme and can be changed in Settings. */
    static Color AccentColor => Appearance.AccentColor;

    static readonly SolidColorBrush RowDivider = new(Color.FromArgb(0xFF, 0x1A, 0x1A, 0x1A));
    /** Row hover highlight: the same wave as the selection, but grey. */
    static readonly Brush RowHover = WaveBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
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

        // The accent only changes computed elements (checkmarks, row highlights);
        // theme brushes update themselves.
        UiEvents.AppearanceChanged += () => DispatcherQueue.TryEnqueue(() =>
        {
            if (!IsLoaded) return;
            Reload();
            ApplyManageLayout(animate: false);
        });

        // The row width is set explicitly from the panel so the star-sized Grid
        // columns always fill it completely, regardless of content.
        ManagePanel.SizeChanged += (_, _) =>
        {
            if (ManagePanel.ActualWidth > 0)
            {
                StopManageWidthTimer();
                ApplyManageLayout(animate: false);
            }
        };
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
        LauncherLabel.Text = s["dash.launch"];
        GameInput.PlaceholderText = s["dash.serverId"];
        PlayerInput.PlaceholderText = s["dash.playerNick"];
        SearchBox.PlaceholderText = s["dash.search"];
        LogoutButton.Content = s["dash.logout"];

        ToolTipService.SetToolTip(RefreshButton, s["dash.refresh"]);
        ToolTipService.SetToolTip(DeleteButton, s["dash.delete"]);
        ToolTipService.SetToolTip(DashboardSettingsButton, s["landing.settings"]);
        ToolTipService.SetToolTip(SearchBox, s["dash.search"]);
        _addWindow?.ApplyLanguage();
        UpdateSortIcons();
        Reload();
    }

    /** Closes the Add account window (called when the main window closes). */
    public void CloseDialogs()
    {
        try { _addWindow?.Close(); } catch { }
        _addWindow = null;
        _dialogOpen = false;
    }

    /* ---------- table ---------- */

    public void Reload()
    {
        // Pull the current list and drop selections for deleted accounts.
        List<AccountInfo> accounts;
        try { accounts = Vault.All()?.ToList() ?? new List<AccountInfo>(); }
        catch (Exception ex)
        {
            App.Dump("DashboardLoad", ex);
            accounts = new List<AccountInfo>();
        }

        var alive = accounts.Select(a => a.Id).ToHashSet();
        _selected.RemoveWhere(id => !alive.Contains(id));
        if (_selected.Count != 1)
        {
            if (_manageMode != null) _animateManageCollapse = true;
            _manageMode = null;
        }

        AccountList.Children.Clear();
        AccountsTitle.Text = $"{Strings.I["dash.accounts"]} ({accounts.Count})";

        _view.Clear();
        _view.AddRange(BuildView(accounts));

        if (_view.Count == 0)
        {
            AccountList.Children.Add(new TextBlock
            {
                Text = Strings.I[accounts.Count == 0 ? "dash.empty" : "dash.notFound"],
                Style = (Style)Application.Current.Resources["AppMutedText"],
                Margin = new Thickness(12, 18, 12, 12),
            });
        }
        else
        {
            for (int i = 0; i < _view.Count; i++)
                AccountList.Children.Add(BuildRow(_view[i], i));
        }

        AccountList.Children.Add(BuildSpacer());
        UpdateControls(accounts);
    }

    /** Favourites always come first; within groups the chosen sort applies. */
    List<AccountInfo> BuildView(List<AccountInfo> all)
    {
        var query = (_query ?? "").Trim();
        var result = new List<AccountInfo>();

        if (!string.IsNullOrEmpty(query))
            all = all.Where(a =>
                (a.Name ?? "").Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();

        result.AddRange(ApplySort(all.Where(a => a.Favorite)));
        result.AddRange(ApplySort(all.Where(a => !a.Favorite)));
        return result;
    }

    IEnumerable<AccountInfo> ApplySort(IEnumerable<AccountInfo> source)
    {
        if (string.IsNullOrEmpty(_sortColumn)) return source;

        return _sortColumn switch
        {
            "name" => _sortDesc
                ? source.OrderByDescending(a => a.Name ?? "", StringComparer.CurrentCultureIgnoreCase)
                : source.OrderBy(a => a.Name ?? "", StringComparer.CurrentCultureIgnoreCase),
            "status" => _sortDesc
                ? source.OrderByDescending(a => a.Online).ThenBy(a => a.Name ?? "")
                : source.OrderBy(a => a.Online).ThenBy(a => a.Name ?? ""),
            "last" => _sortDesc
                ? source.OrderByDescending(LastOnlineKey).ThenBy(a => a.Name ?? "")
                : source.OrderBy(LastOnlineKey).ThenBy(a => a.Name ?? ""),
            "data" => _sortDesc
                ? source.OrderByDescending(a => a.DataOk).ThenBy(a => a.Name ?? "")
                : source.OrderBy(a => a.DataOk).ThenBy(a => a.Name ?? ""),
            _ => source,
        };
    }

    static DateTime LastOnlineKey(AccountInfo account) =>
        DateTime.TryParseExact(account.LastOnline ?? "", "dd.MM.yyyy HH:mm",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var value)
            ? value
            : DateTime.MinValue;

    /* ---------- search and sorting ---------- */

    void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _query = SearchBox.Text ?? "";
        Reload();
    }

    void OnSortName(object sender, TappedRoutedEventArgs e) { e.Handled = true; SetSort("name"); }
    void OnSortStatus(object sender, TappedRoutedEventArgs e) { e.Handled = true; SetSort("status"); }
    void OnSortLast(object sender, TappedRoutedEventArgs e) { e.Handled = true; SetSort("last"); }
    void OnSortData(object sender, TappedRoutedEventArgs e) { e.Handled = true; SetSort("data"); }

    void SetSort(string column)
    {
        if (_sortColumn == column)
        {
            _sortDesc = !_sortDesc;
        }
        else
        {
            _sortColumn = column;
            // For status, date and data, "online/newest/current first" is more useful.
            _sortDesc = column is "status" or "last" or "data";
        }
        UpdateSortIcons();
        Reload();
    }

    void UpdateSortIcons()
    {
        var icons = new (string Column, FontIcon Icon)[]
        {
            ("name", SortNameIcon),
            ("status", SortStatusIcon),
            ("last", SortLastIcon),
            ("data", SortDataIcon),
        };
        foreach (var (column, icon) in icons)
        {
            if (icon == null) continue;
            bool active = _sortColumn == column;
            icon.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            if (active) icon.Glyph = _sortDesc ? "\uE70D" : "\uE70E";
        }
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
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });

        // Checkbox
        var box = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1.5),
            BorderBrush = selected ? new SolidColorBrush(AccentColor) : CheckBorder,
            Background = selected ? new SolidColorBrush(AccentColor) : new SolidColorBrush(Colors.Transparent),
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

        // Favourite: star in the last column.
        var star = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Colors.Transparent),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new FontIcon
            {
                Glyph = account.Favorite ? "\uE735" : "\uE734",
                FontSize = 16,
                Foreground = account.Favorite ? FavoriteGold : MutedBrush,
            },
        };
        ToolTipService.SetToolTip(star, Strings.I[account.Favorite ? "dash.unfavorite" : "dash.favorite"]);
        star.Tapped += (s, e) =>
        {
            e.Handled = true;
            account.Favorite = !account.Favorite;
            Vault.Save();
            Reload();
        };
        Grid.SetColumn(star, 5);
        grid.Children.Add(star);

        var row = new Border
        {
            Padding = new Thickness(12, 13, 10, 13),
            BorderBrush = RowDivider,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = selected ? SelectionBrush()
                : account.Favorite ? FavoriteBrush()
                : new SolidColorBrush(Colors.Transparent),
            Child = grid,
            Tag = account.Id,
        };
        row.Tapped += (s, e) =>
        {
            // A row click must not reach the panel; otherwise the empty-area
            // handler would clear the selection immediately.
            e.Handled = true;
            OnRowTapped(account.Id, index);
        };
        row.PointerEntered += (s, e) => { if (!_selected.Contains(account.Id)) row.Background = RowHover; };
        row.PointerExited += (s, e) =>
        {
            if (_selected.Contains(account.Id)) return;
            row.Background = account.Favorite ? FavoriteBrush() : new SolidColorBrush(Colors.Transparent);
        };
        return row;
    }

    static readonly SolidColorBrush FavoriteGold = new(Color.FromArgb(0xFF, 0xE8, 0xC4, 0x6A));

    /** A light golden wave for favourite accounts. */
    static LinearGradientBrush FavoriteBrush() => WaveBrush(Color.FromArgb(0x2E, 0xE0, 0xBA, 0x5E));

    static LinearGradientBrush SelectionBrush()
    {
        var accent = AccentColor;
        return WaveBrush(Color.FromArgb(0x33, accent.R, accent.G, accent.B));
    }

    /**
     * Horizontal wave with transparent edges, like the 2.1.0 selection.
     * The caller sets the colour: blue for selection, white/grey for hover.
     */
    static LinearGradientBrush WaveBrush(Color color)
    {
        byte a = color.A;
        return new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5),
            GradientStops =
            {
                new GradientStop { Color = Color.FromArgb(0, color.R, color.G, color.B), Offset = 0 },
                new GradientStop { Color = Color.FromArgb(a, color.R, color.G, color.B), Offset = 0.15 },
                new GradientStop { Color = Color.FromArgb(a, color.R, color.G, color.B), Offset = 0.85 },
                new GradientStop { Color = Color.FromArgb(0, color.R, color.G, color.B), Offset = 1 },
            },
        };
    }

    /** Clicking an empty spot in the account panel clears the selection. */
    void OnAccountsPanelTapped(object sender, TappedRoutedEventArgs e)
    {
        if (_selected.Count == 0) return;

        _selected.Clear();
        _lastIndex = -1;

        // An expanded drawer collapses with the selection, smoothly.
        if (_manageMode != null)
        {
            _manageMode = null;
            _animateManageCollapse = true;
        }

        UpdateStatusText();
        Reload();
    }

    void OnRowTapped(long id, int index)
    {        bool additive = IsCtrlDown();
        bool range = IsShiftDown();

        if (additive)
        {
            if (!_selected.Remove(id)) _selected.Add(id);
            _lastIndex = index;
        }
        else if (range && _lastIndex >= 0)
        {
            int start = Math.Min(_lastIndex, index);
            int end = Math.Max(_lastIndex, index);
            _selected.Clear();
            for (int i = start; i <= end && i < _view.Count; i++) _selected.Add(_view[i].Id);
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

        if (_selected.Count != 1)
        {
            // Any selection reset (checkbox, Ctrl+click, empty-area click)
            // collapses the expanded drawer smoothly instead of snapping.
            if (_manageMode != null) _animateManageCollapse = true;
            _manageMode = null;
        }
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

    static readonly SolidColorBrush ManageTitleActive = new(Color.FromArgb(0xFF, 0x88, 0x88, 0x88));
    static readonly SolidColorBrush ManageTitleDisabled = new(Color.FromArgb(0xFF, 0x55, 0x55, 0x55));
    static readonly SolidColorBrush SegmentBg = new(Color.FromArgb(0x8C, 0x1A, 0x1C, 0x24));
    static readonly SolidColorBrush SegmentBorder = new(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF));
    static readonly SolidColorBrush SegmentBgActive = new(Color.FromArgb(0xB3, 0x0F, 0x11, 0x18));
    /** The active drawer border also follows the accent. */
    static SolidColorBrush SegmentBorderActive => new(AccentColor);

    /* ---------- responsive grid (like the 2.1.0 media queries) ---------- */

    void ApplyResponsiveLayout()
    {
        double w = ActualWidth;
        if (w >= 1100)
        {
            DashBody.Padding = new Thickness(28, 24, 28, 24);
            DashBody.ColumnSpacing = 20;
            DashHeader.Padding = new Thickness(28, 16, 28, 16);
            ColSidebar.Width = new GridLength(360);
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
            // Wider than the previous 280 px: an expanded drawer must fit the input field.
            ColSidebar.Width = new GridLength(340);
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

        // Changing the selection in the list must not animate drawer widths;
        // animate only on an explicit segment click or when clearing the
        // selection (empty-area click, checkbox, Ctrl+click).
        bool animate = _animateManageCollapse && !canManage;
        _animateManageCollapse = false;

        // The legacy version dimmed only the panel's children, not the panel itself.
        FadeTo(ManageInner, canManage ? 1 : 0.4, 300, animate);
        ManageInner.IsHitTestVisible = canManage;
        ManageTitle.Foreground = canManage ? ManageTitleActive : ManageTitleDisabled;
        if (!canManage) ClearManageInputs();

        ApplyManageLayout(animate: animate);
    }

    void ApplyManageLayout(bool animate = true)
    {
        bool game = _manageMode == "game";
        bool player = _manageMode == "player";
        bool launcher = _manageMode == "launcher";

        double rowWidth = ManagePanel.ActualWidth - ManagePanel.Padding.Left - ManagePanel.Padding.Right;
        if (rowWidth > 0 && (double.IsNaN(ManageRow.Width) || Math.Abs(ManageRow.Width - rowWidth) > 0.5))
            ManageRow.Width = rowWidth;

        // flex: 1 for inactive drawers and flex: 3 for the active one (do.html).
        if (animate) AnimateManageColumns(game, player, launcher);
        else
        {
            StopManageWidthTimer();
            SetManageColumns(game, player, launcher);
        }

        SegmentBgTo(GameSegment, game);
        SegmentBgTo(PlayerSegment, player);
        SegmentBgTo(LauncherSegment, launcher);

        // The icon fades out while the content fades in.
        ApplySegmentState(GameIcon, GameContent, game, animate);
        ApplySegmentState(PlayerIcon, PlayerContent, player, animate);
        ApplySegmentState(LauncherIcon, LauncherContent, launcher, animate);
    }

    static void SegmentBgTo(Border segment, bool active)
    {
        segment.Background = active ? SegmentBgActive : SegmentBg;
        segment.BorderBrush = active ? SegmentBorderActive : SegmentBorder;
    }

    static void ApplySegmentState(FrameworkElement icon, FrameworkElement content, bool active, bool animate)
    {
        content.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        if (active)
        {
            content.Opacity = 0;
            FadeTo(content, 1, 300, animate);
        }
        else
        {
            content.Opacity = 0;
        }

        FadeTo(icon, active ? 0 : 1, 200, animate);
    }

    DispatcherTimer _manageWidthTimer;

    /**
     * Current drawer "flex-grow" values: 1 when collapsed, 3 when expanded.
     * These coefficients are animated, like the CSS flex transition in the demo:
     * the Grid recalculates widths itself, so the reverse animation is a true
     * reversal (active 3->1, others 1->1 but growing by share).
     */
    readonly double[] _manageGrow = { 1, 1, 1 };

    void AnimateManageColumns(bool game, bool player, bool launcher)
    {
        var to = new[] { game ? 3.0 : 1.0, player ? 3.0 : 1.0, launcher ? 3.0 : 1.0 };
        var from = new[] { _manageGrow[0], _manageGrow[1], _manageGrow[2] };

        if (!ManageRow.IsLoaded)
        {
            StopManageWidthTimer();
            SetManageColumns(game, player, launcher);
            return;
        }

        StopManageWidthTimer();
        var started = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _manageWidthTimer = timer;
        timer.Tick += (_, _) =>
        {
            double t = Math.Clamp((DateTime.UtcNow - started).TotalMilliseconds / 300.0, 0, 1);
            // cubic-bezier(0.25, 0.8, 0.25, 1) ≈ EaseOutCubic.
            double e = 1 - Math.Pow(1 - t, 3);
            for (int i = 0; i < 3; i++)
                SetManageGrow(i, from[i] + (to[i] - from[i]) * e);

            if (t >= 1)
            {
                StopManageWidthTimer();
                SetManageColumns(game, player, launcher);
            }
        };
        timer.Start();
    }

    void SetManageGrow(int index, double grow)
    {
        _manageGrow[index] = grow;
        var width = new GridLength(grow, GridUnitType.Star);
        switch (index)
        {
            case 0: ManageColGame.Width = width; break;
            case 1: ManageColPlayer.Width = width; break;
            default: ManageColLauncher.Width = width; break;
        }
    }

    void StopManageWidthTimer()
    {
        _manageWidthTimer?.Stop();
        _manageWidthTimer = null;
    }

    void SetManageColumns(bool game, bool player, bool launcher)
    {
        SetManageGrow(0, game ? 3 : 1);
        SetManageGrow(1, player ? 3 : 1);
        SetManageGrow(2, launcher ? 3 : 1);
    }

    static void FadeTo(UIElement element, double to, int milliseconds, bool animate)
    {
        if (!animate)
        {
            element.Opacity = to;
            return;
        }

        var anim = new DoubleAnimation
        {
            To = to,
            Duration = TimeSpan.FromMilliseconds(milliseconds),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(anim, element);
        Storyboard.SetTargetProperty(anim, "Opacity");
        var sb = new Storyboard();
        sb.Children.Add(anim);
        sb.Begin();
    }

    void ClearManageInputs()
    {
        GameInput.Text = "";
        PlayerInput.Text = "";
    }

    /* ---------- actions ---------- */

    void OnGameSegmentTapped(object sender, TappedRoutedEventArgs e) =>
        ToggleManageMode("game");

    void OnPlayerSegmentTapped(object sender, TappedRoutedEventArgs e) =>
        ToggleManageMode("player");

    void OnLauncherSegmentTapped(object sender, TappedRoutedEventArgs e)
    {
        if (_selected.Count != 1 || _manageMode == "launcher") return;
        _manageMode = "launcher";
        ClearManageInputs();
        ApplyManageLayout();
    }

    void OnManageContentTapped(object sender, TappedRoutedEventArgs e) => e.Handled = true;

    void ToggleManageMode(string mode)
    {
        if (_selected.Count != 1 || _manageMode == mode) return;

        _manageMode = mode;
        ClearManageInputs();
        ApplyManageLayout();

        if (mode == "game") FocusManageInput(GameInput, mode);
        else if (mode == "player") FocusManageInput(PlayerInput, mode);
    }

    async void FocusManageInput(TextBox input, string mode)
    {
        // In do.html the focus is set after the expand animation finishes.
        await Task.Delay(320);
        if (_manageMode == mode) input.Focus(FocusState.Programmatic);
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

    /* ---------- adding an account ---------- */

    void OnAddAccount(object sender, RoutedEventArgs e)
    {
        if (_dialogOpen) return;
        _dialogOpen = true;

        // The add form is a separate window, not a layer over the dashboard.
        var dialog = new AddAccountWindow();
        _addWindow = dialog;
        dialog.BrowserRequested = () =>
        {
            dialog.Close();
            _ = AddViaBrowser();
        };
        dialog.CookieRequested = value =>
        {
            dialog.Close();
            _ = AddByCookie(value);
        };
        dialog.Closed += (_, _) =>
        {
            _addWindow = null;
            _dialogOpen = false;
        };
        dialog.Activate();
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
            var login = new BrowserLoginWindow();
            var cookie = await login.ShowAsync();
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

    /* ---------- deletion ---------- */

    async void OnDeleteAccounts(object sender, RoutedEventArgs e)
    {
        if (_selected.Count == 0 || _dialogOpen) return;
        _dialogOpen = true;

        var ids = _selected.ToArray();
        try
        {
            var names = ids.Select(id => Vault.Find(id)?.Name ?? "—").ToList();
            var dialog = new DeleteAccountWindow(names);
            if (!await dialog.ShowAsync()) return;

            Vault.Remove(ids);
            Vault.Save();
            _selected.Clear();
            _manageMode = null;
            Reload();
            Status(Strings.I.F("dash.deleted", ids.Length), "success");
        }
        catch (Exception ex)
        {
            App.Dump("DeleteAccounts", ex);
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    /* ---------- misc ---------- */

    void OnOpenSettings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();
    void OnLogout(object sender, RoutedEventArgs e) => LogoutRequested?.Invoke();

    static void Status(string text, string kind) => CoreHost.PushStatus(text, kind);
}
