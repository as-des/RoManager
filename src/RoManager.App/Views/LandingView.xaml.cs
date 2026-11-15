using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace RoManager.App.Views;

public sealed partial class LandingView : UserControl
{
    public event Action<bool> StartRequested;

    public LandingView()
    {
        InitializeComponent();
        ApplyLanguage();
    }

    public void ApplyLanguage()
    {
        var s = Strings.I;
        Subtitle.Text = s["landing.subtitle"];
        HaveAccessButton.Content = s["landing.haveAccess"];
        NoAccessButton.Content = s["landing.noAccess"];
        Hint.Text = s["landing.hint"];
    }

    void OnHaveAccess(object sender, RoutedEventArgs e) => StartRequested?.Invoke(true);
    void OnNoAccess(object sender, RoutedEventArgs e) => StartRequested?.Invoke(false);
}
