using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using RoManager.Core;
using Windows.Graphics;

namespace RoManager.App;

/**
 * Standalone add-account window. The form used to be drawn over the
 * dashboard (ModalOverlay), adding an extra UI layer; it is now a
 * separate window, like Settings.
 *
 * The window holds no business logic: it only collects input and invokes
 * callbacks, while DashboardView adds accounts by cookie or through the browser.
 */
public sealed partial class AddAccountWindow : Window
{
    const int Width = 460;
    const int Height = 312;

    public Action BrowserRequested { get; set; }
    public Action<string> CookieRequested { get; set; }

    public AddAccountWindow()
    {
        InitializeComponent();

        var s = Strings.I;
        Title = s["add.title"];
        BrowserText.Text = s["add.browser"];
        OrText.Text = s["add.or"];
        CookieBox.PlaceholderText = s["add.cookie"];
        CookieButton.Content = s["add.byCookie"];
        CancelButton.Content = s["common.cancel"];

        WindowChrome.ApplyDark(this);
        // Owned by the main window: it never falls behind it and closes with it.
        WindowChrome.SetOwner(this, App.Window);

        if (AppWindow is { } window)
        {
            window.Resize(new SizeInt32(Width, Height));
            var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
            if (File.Exists(iconPath)) window.SetIcon(iconPath);
            if (window.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
            }

            // Centred on the main window, like a regular dialog.
            if (App.Window?.AppWindow is { } main)
            {
                int x = main.Position.X + (main.Size.Width - Width) / 2;
                int y = main.Position.Y + (main.Size.Height - Height) / 2;
                window.Move(new PointInt32(Math.Max(0, x), Math.Max(0, y)));
            }
        }

        CookieBox.Loaded += (_, _) => CookieBox.Focus(FocusState.Programmatic);
    }

    public void ApplyLanguage()
    {
        var s = Strings.I;
        Title = s["add.title"];
        BrowserText.Text = s["add.browser"];
        OrText.Text = s["add.or"];
        CookieBox.PlaceholderText = s["add.cookie"];
        CookieButton.Content = s["add.byCookie"];
        CancelButton.Content = s["common.cancel"];
    }

    void OnBrowser(object sender, RoutedEventArgs e) => BrowserRequested?.Invoke();

    void OnCookie(object sender, RoutedEventArgs e)
    {
        var value = CookieBox.Password;
        CookieBox.Password = "";
        CookieRequested?.Invoke(value);
    }

    void OnCancel(object sender, RoutedEventArgs e) => Close();
}
