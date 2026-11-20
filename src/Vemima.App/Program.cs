using Avalonia;

namespace Vemima.App;

class Program
{
    /// <summary>Startet die Desktop-App (klassischer Avalonia-Desktop-Lifetime).</summary>
    [STAThread]
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Konfiguriert Avalonia (Plattform, Schrift, Logging); wird auch vom Designer genutzt.</summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
