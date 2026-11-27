using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace RoManager.App.Controls;

/**
 * Notification cards above the window content, a port of NotificationStack.tsx (2.1.0).
 *
 * New cards stack on top of older ones, so only the edges of the lower cards show.
 * There is one notification per text (repeating it refreshes the existing card),
 * each with its own lifetime: success 5 s, info 6 s, warning 9 s, danger 12 s.
 */
public sealed class NotificationStack : Grid
{
    sealed class Item
    {
        public int Id;
        public string Text;
        public FrameworkElement Card;
        public FrameworkElement Inner;
        public DispatcherTimer Timer;
    }

    readonly List<Item> _items = new();
    int _seq;

    public NotificationStack()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(10, 8, 10, 0);
        IsHitTestVisible = true;
        Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
    }

    public void Notify(string text, string kind = "info", string title = null,
                       (string Label, Action Action)[] actions = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        kind = Normalize(kind);

        string trimmed = text.Trim();
        var existing = _items.FirstOrDefault(i => i.Text == trimmed);
        int id = existing?.Id ?? ++_seq;

        if (existing != null) Remove(existing, animate: false, silent: true);

        var (card, inner) = BuildCard(id, trimmed, kind, title, actions);
        var item = new Item { Id = id, Text = trimmed, Card = card, Inner = inner };
        _items.Insert(0, item);
        Children.Add(card);

        while (_items.Count > 4) Remove(_items[^1], animate: false, silent: true);

        var lifetime = kind switch
        {
            "success" => 5000,
            "info" => 6000,
            "warning" => 9000,
            _ => 12000,
        };
        item.Timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(lifetime) };
        item.Timer.Tick += (_, _) => Remove(item, animate: true);
        item.Timer.Start();

        Layout();
    }

    static string Normalize(string kind) => kind switch
    {
        "success" or "danger" or "warning" or "info" => kind,
        _ => "info",
    };

    static Color Accent(string kind) => kind switch
    {
        "success" => Color.FromArgb(0xFF, 0x22, 0xC5, 0x5E),
        "danger" => Color.FromArgb(0xFF, 0xEF, 0x44, 0x44),
        "warning" => Color.FromArgb(0xFF, 0xEA, 0xB3, 0x08),
        _ => Appearance.AccentColor,
    };

    static string Glyph(string kind) => kind switch
    {
        "success" => "\uE73E",
        "danger" => "\uEA39",
        "warning" => "\uE7BA",
        _ => "\uE946",
    };

    (FrameworkElement Card, FrameworkElement Inner) BuildCard(int id, string text, string kind, string title,
                                                              (string Label, Action Action)[] actions)
    {
        var accent = new SolidColorBrush(Accent(kind));

        var grid = new Grid { Padding = new Thickness(14, 10, 12, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new FontIcon
        {
            Glyph = Glyph(kind),
            FontSize = 17,
            Foreground = accent,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var copy = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        if (!string.IsNullOrWhiteSpace(title))
        {
            copy.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)),
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        copy.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xD0, 0xD5, 0xDE)),
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
        });
        Grid.SetColumn(copy, 1);
        grid.Children.Add(copy);

        if (actions is { Length: > 0 })
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
            };
            foreach (var (label, action) in actions)
            {
                var button = new Button
                {
                    Style = (Style)Application.Current.Resources["AppNoticeAction"],
                    Content = label,
                };
                button.Click += (_, _) => action?.Invoke();
                row.Children.Add(button);
            }
            Grid.SetColumn(row, 2);
            grid.Children.Add(row);
        }

        var close = new Button
        {
            Style = (Style)Application.Current.Resources["AppNoticeClose"],
            Content = new FontIcon { Glyph = "\uE711", FontSize = 10 },
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        close.Click += (_, _) =>
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item != null) Remove(item, animate: true);
        };
        Grid.SetColumn(close, 3);
        grid.Children.Add(close);

        var card = new GlassSurface
        {
            Style = (Style)Application.Current.Resources["AppGlassNotice"],
            Content = grid,
        };

        return (card, grid);
    }

    void Remove(Item item, bool animate, bool silent = false)
    {
        item.Timer?.Stop();
        if (!_items.Remove(item)) return;

        Children.Remove(item.Card);
        Layout();
    }

    void Layout()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            item.Card.Margin = new Thickness(0, i * 8, 0, 0);
            item.Card.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0);
            item.Card.RenderTransform = new ScaleTransform
            {
                ScaleX = 1 - i * 0.012,
                ScaleY = 1 - i * 0.012,
            };
            Canvas.SetZIndex(item.Card, 1000 - i);
            item.Inner.Visibility = i == 0 ? Visibility.Visible : Visibility.Collapsed;
            item.Card.IsHitTestVisible = i == 0;
        }
    }
}
