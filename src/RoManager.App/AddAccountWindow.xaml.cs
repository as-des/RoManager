using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using RoManager.Core;
using Windows.Graphics;

namespace RoManager.App;

/**
 * Отдельное окно добавления аккаунта. Раньше форма рисовалась поверх
 * дашборда (ModalOverlay) и создавала лишний слой интерфейса — теперь это
 * самостоятельное окно, как окно настроек.
 *
 * Окно не содержит бизнес-логики: оно только собирает ввод и дергает
 * колбэки, а добавление по cookie/через браузер выполняет DashboardView.
 */
public sealed partial class AddAccountWindow : Window
{
    const int Width = 460;
    const int Height = 424;

    public Action BrowserRequested { get; set; }
    public Action<string> CookieRequested { get; set; }

    public AddAccountWindow()
    {
        InitializeComponent();

        var s = Strings.I;
        Title = s["add.title"];
        TitleText.Text = s["add.title"];
        HintText.Text = s["add.hint"];
        BrowserText.Text = s["add.browser"];
        CookieBox.PlaceholderText = s["add.cookie"];
        CookieButton.Content = s["add.byCookie"];
        CancelButton.Content = s["common.cancel"];

        WindowChrome.ApplyDark(this);

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

            // По центру главного окна, как обычный диалог.
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
        TitleText.Text = s["add.title"];
        HintText.Text = s["add.hint"];
        BrowserText.Text = s["add.browser"];
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
