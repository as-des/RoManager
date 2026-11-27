using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using RoManager.App.Localization;
using Windows.Graphics;

namespace RoManager.App;

/**
 * Standalone account-deletion confirmation window, as independent as the
 * add-account and Settings windows. The confirmation used to be drawn over the
 * dashboard, mixing two different UI layers.
 *
 * The window only asks for confirmation; DashboardView performs the deletion.
 */
public sealed partial class DeleteAccountWindow : Window
{
    const int Width = 460;
    const int Height = 250;

    readonly TaskCompletionSource<bool> _result =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    bool _done;

    public DeleteAccountWindow(IReadOnlyList<string> names)
    {
        InitializeComponent();

        var s = Strings.I;
        Title = s["dash.deleteHeading"];
        HeadingText.Text = s["dash.deleteHeading"];
        BodyText.Text = names.Count == 1
            ? s.F("dash.deleteOne", names[0])
            : s.F("dash.deleteMany", string.Join(", ", names));
        DeleteButton.Content = s["dash.delete"];
        CancelButton.Content = s["common.cancel"];

        WindowChrome.ApplyDark(this);
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

            if (App.Window?.AppWindow is { } main)
            {
                int x = main.Position.X + (main.Size.Width - Width) / 2;
                int y = main.Position.Y + (main.Size.Height - Height) / 2;
                window.Move(new PointInt32(Math.Max(0, x), Math.Max(0, y)));
            }
        }

        Closed += (_, _) => Finish(false);
    }

    /** true when the user confirmed deletion; false on cancel or close. */
    public Task<bool> ShowAsync()
    {
        Activate();
        return _result.Task;
    }

    void Finish(bool confirmed)
    {
        if (_done) return;
        _done = true;
        _result.TrySetResult(confirmed);
    }

    void OnDelete(object sender, RoutedEventArgs e)
    {
        Finish(true);
        Close();
    }

    void OnCancel(object sender, RoutedEventArgs e) => Close();
}
