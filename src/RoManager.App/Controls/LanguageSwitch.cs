using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RoManager.Core;
using Windows.UI;

namespace RoManager.App.Controls;

/** RU/EN switch as in 2.1.0 (one per screen). */
public sealed class LanguageSwitch : UserControl
{
    static readonly SolidColorBrush ActiveBg = new(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF));
    static readonly SolidColorBrush ActiveFg = new(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
    static readonly SolidColorBrush IdleFg = new(Color.FromArgb(0xFF, 0x8A, 0x92, 0xA2));

    readonly Button _ru;
    readonly Button _en;

    public LanguageSwitch()
    {
        var frame = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
        };
        // The host screen controls placement (left/right), not the switch itself.
        VerticalAlignment = VerticalAlignment.Center;

        var style = (Style)Application.Current.Resources["AppLangButton"];
        _ru = new Button { Style = style, Content = "RU", MinWidth = 32 };
        _en = new Button { Style = style, Content = "EN", MinWidth = 32 };
        _ru.Click += (_, _) => SetLanguage("ru");
        _en.Click += (_, _) => SetLanguage("en");

        frame.Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { _ru, _en },
        };
        Content = frame;
        ApplyLanguage();
    }

    public void ApplyLanguage()
    {
        bool en = Loc.IsEnglish;
        _en.Background = en ? ActiveBg : null;
        _en.Foreground = en ? (Brush)ActiveFg : IdleFg;
        _ru.Background = en ? null : ActiveBg;
        _ru.Foreground = en ? IdleFg : (Brush)ActiveFg;
    }

    public static void SetLanguage(string lang)
    {
        if (Loc.Language == lang) return;
        Loc.Set(lang);
        UiEvents.RaiseLanguageChanged();
    }
}
