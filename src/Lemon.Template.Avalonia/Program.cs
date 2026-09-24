using Avalonia;

namespace Lemon.Template.Avalonia;

internal static class Program
{
    /// <summary>
    /// Entry point. Nothing that touches Avalonia may run before <see cref="AppBuilder"/> has been
    /// configured, so all start-up work lives in <see cref="App.OnFrameworkInitializationCompleted"/>.
    /// </summary>
    [STAThread]
    public static int Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Also used by the XAML previewer, which is why it must stay public and side-effect free.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
