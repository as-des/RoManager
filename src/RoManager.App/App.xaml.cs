using Microsoft.UI.Xaml;
using RoManager.Core;

namespace RoManager.App;

public partial class App : Application
{
    public static MainWindow Window { get; private set; }

    public App()
    {
        InitializeComponent();

        UnhandledException += (_, e) => Dump("XamlUnhandled", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Dump("AppDomain", e.ExceptionObject as Exception);
    }

    public static void Dump(string tag, Exception ex)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "winui-crash.log");
            File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss}] {tag}\n{ex}\n\n");
        }
        catch { }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            Loc.Load();
            // Background settings (colours, speed, renderer) are loaded before the window:
            // otherwise shader.json only applied after Settings was opened.
            ShaderConfig.Load();
            // Accent and background blur are also applied before the window; otherwise
            // the first frame uses the default blue and waits to be repainted.
            AppearanceConfig.Load();
            AppearanceConfig.Changed += Appearance.Apply;
            Appearance.Apply();
            // The core does not create a session itself; the UI layer does it at startup (like MainFrame in 2.1.0).
            CoreHost.Session = new VaultSession();
            Window = new MainWindow();
            Window.Activate();
        }
        catch (Exception ex)
        {
            Dump("OnLaunched", ex);
            throw;
        }
    }
}
