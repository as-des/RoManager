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
            // Настройки фона (цвета, скорость, renderer) читаются до окна:
            // без этого shader.json применялся только после открытия настроек.
            ShaderConfig.Load();
            // Акцент и размытие фона — тоже до окна, иначе первый кадр
            // рисуется дефолтным синим и приходится ждать перерисовки.
            AppearanceConfig.Load();
            AppearanceConfig.Changed += Appearance.Apply;
            Appearance.Apply();
            // Ядро не создаёт сессию само: это делает UI-слой при старте (как MainFrame в 2.1.0).
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
