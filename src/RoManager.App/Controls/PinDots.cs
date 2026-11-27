using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace RoManager.App.Controls;

/** Six PIN dots as in 2.1.0: empty dots are outlines, entered dots are filled. */
public sealed class PinDots : StackPanel
{
    static readonly SolidColorBrush EmptyBorder = new(Color.FromArgb(0xFF, 0x44, 0x44, 0x44));

    /** Filled dots use the accent colour from Settings. */
    static SolidColorBrush Filled => new(Appearance.AccentColor);

    readonly Border[] _dots = new Border[6];

    public PinDots()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 10;
        HorizontalAlignment = HorizontalAlignment.Center;
        Margin = new Thickness(0, 0, 0, 18);

        for (int i = 0; i < _dots.Length; i++)
        {
            var dot = new Border
            {
                Width = 12,
                Height = 12,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1.5),
                BorderBrush = EmptyBorder,
                Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            };
            _dots[i] = dot;
            Children.Add(dot);
        }
    }

    public void SetCount(int count)
    {
        for (int i = 0; i < _dots.Length; i++)
        {
            bool on = i < count;
            var filled = Filled;
            _dots[i].Background = on ? filled : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            _dots[i].BorderBrush = on ? filled : EmptyBorder;
        }
    }
}

