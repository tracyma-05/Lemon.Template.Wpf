using Avalonia;
using Lemon.Template.Avalonia.Services.Updates;

namespace Lemon.Template.Avalonia;

internal static class Program
{
    /// <summary>
    /// Entry point. Nothing that touches Avalonia may run before <see cref="AppBuilder"/> has been
    /// configured, so all start-up work lives in <see cref="App.OnFrameworkInitializationCompleted"/>.
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        // Started by the previous version to install this one (see UpdateInstaller): swap the files, start
        // the installed app and exit, without initializing Avalonia at all.
        if (UpdateApplier.TryRun(args, out var updaterExitCode))
        {
            return updaterExitCode;
        }

        // Child processes inherit the working directory; one left inside the install folder (a browser
        // opened from the app, a terminal) keeps it in use and the next update cannot swap the folder.
        AppInstallation.MoveWorkingDirectoryOut();

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Also used by the XAML previewer, which is why it must stay public and side-effect free.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
