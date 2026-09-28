#if (EnableTrayIcon || EnableDesktopShortcut)
using Lemon.Template.Wpf.Infrastructures.Shell;
#endif
using Lemon.Template.Wpf.Infrastructures;
using Lemon.Template.Wpf.Infrastructures.Attributes;
using Lemon.Template.Wpf.Infrastructures.Exceptions;
using Lemon.Template.Wpf.Infrastructures.Localization;
using Lemon.Template.Wpf.Services.Localization;
using Lemon.Template.Wpf.Services.Theming;
using Lemon.Template.Wpf.Services.Updates;
using Lemon.Template.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Volo.Abp;

namespace Lemon.Template.Wpf;

public partial class App : Application
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(10);

    private IAbpApplicationWithInternalServiceProvider? _abpApplication;

    private static IServiceProvider? _serviceProvider;

    /// <summary>
    /// Set once the application is really quitting (<see cref="Quit"/>, Windows sign-out), so the main window
    /// lets itself close instead of hiding in the tray.
    /// </summary>
    internal static bool IsExiting { get; private set; }

    /// <summary>
    /// Quits the application. Use this rather than <c>Application.Current.Shutdown()</c>: it also tells the main
    /// window not to hide in the tray. Shutdown() runs <see cref="OnExit"/> (ABP shutdown, Hangfire stop, Serilog
    /// flush); Environment.Exit would skip all of it.
    /// </summary>
    internal static void Quit()
    {
        IsExiting = true;
        Current.Shutdown();
    }

    /// <summary>
    /// Ambient container for the few WPF extension points that cannot take constructor injection
    /// (attached-property callbacks, class handlers). Use constructor injection everywhere else.
    /// </summary>
    internal static IServiceProvider ServiceProvider =>
        _serviceProvider ?? throw new InvalidOperationException(
            "Application services are not available yet: the ABP host has not finished initializing.");

    /// <summary>
    /// Non-throwing counterpart of <see cref="ServiceProvider"/> for callers that legitimately run
    /// before the host is ready — global class handlers fire for the splash screen and for the
    /// elements the debugger injects (XAML Hot Reload, Live Visual Tree) during startup.
    /// </summary>
    internal static IServiceProvider? ServiceProviderOrNull => _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Started by the previous version to install this one (see UpdateInstaller): swap the files, start
        // the installed app and exit, before any logging, container or window exists.
        if (UpdateApplier.TryRun(e.Args, out var updaterExitCode))
        {
            Shutdown(updaterExitCode);
            return;
        }

        // Child processes inherit the working directory; one left inside the install folder (a browser
        // opened from the app, a terminal) keeps it in use and the next update cannot swap the folder.
        AppInstallation.MoveWorkingDirectoryOut();

#if (EnableTrayIcon)
        // After the updater check: the updater runs while the previous version still holds the lock, and it
        // only starts the new version once that process has exited.
        if (!SingleInstance.TryAcquire())
        {
            SingleInstance.SignalExistingInstance();
            Shutdown(0);
            return;
        }

#endif
        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Information()
#else
            // Release keeps only actionable entries: Information-level chatter dominated the log volume.
            .MinimumLevel.Warning()
#endif
            // Never below the root level, otherwise the framework raises the volume it is meant to cap.
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Async(c => c.File(
                // Absolute: the working directory is not always the app folder, and the
                // Logs → Local-Logs page reads from AppContext.BaseDirectory.
                path: Path.Combine(AppContext.BaseDirectory, "Logs", "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                shared: true,
                encoding: System.Text.Encoding.UTF8))
            .CreateLogger();

        var handler = new ExceptionHandler();
        ExceptionHandler(handler);

        SplashWindow? splash = null;
        try
        {
            Log.Information("Starting WPF host.");

            splash = new SplashWindow();
            splash.Show();
            await splash.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

#if (EnableDesktopShortcut)
            DesktopShortcutHelper.EnsureDesktopShortcut();
#endif

            _abpApplication = await AbpApplicationFactory.CreateAsync<WpfModule>(options =>
            {
                options.UseAutofac();
                options.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));
            });

            await _abpApplication.InitializeAsync();
            var services = _abpApplication.ServiceProvider;
            _serviceProvider = services;

            // Only now that the container exists: the handler is global, so registering it any earlier
            // means every element loaded during startup asks for services that are not there yet.
            ViewModelLocator.EnableAutoWiring();

            ServiceCollectionKeyedExtensions.AddRouteServiceFromAssembly(services, typeof(App).Assembly);

            // Before any view is created, so the first render already uses the chosen language.
            var languageStore = services.GetRequiredService<IAppLanguagePreferencesStore>();
            LocalizationService.Instance.SetCulture(
                LocalizationService.Instance.ResolveSupportedCulture(languageStore.Load()));

            await Dispatcher.InvokeAsync(() =>
            {
                var themeStore = services.GetRequiredService<IAppThemePreferencesStore>();
                var themeService = services.GetRequiredService<IAppThemeService>();
                var snapshot = themeStore.Load();
                if (snapshot is not null)
                {
                    themeService.ApplySnapshot(snapshot);
                }
            });

            var mainWindow = _abpApplication.Services.GetRequiredService<MainWindow>();
            void OnMainContentRendered(object? _, EventArgs __)
            {
                mainWindow.ContentRendered -= OnMainContentRendered;
                splash?.Close();
                splash = null;
            }

            mainWindow.ContentRendered += OnMainContentRendered;
            mainWindow.Show();

            Current.MainWindow = mainWindow;

            // After an automatic update: log what the updater did and remove its backup and staging copy.
            _ = UpdateInstaller.CleanUpAfterUpdateAsync(services.GetRequiredService<ILoggerFactory>().CreateLogger("Updates"));
        }
        catch (Exception ex)
        {
            splash?.Close();
            Log.Fatal(ex, "Host terminated unexpectedly!");

            MessageBox.Show(
                $"The application failed to start:{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "Startup failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            // Without this the process would linger with no window and no way to quit.
            Shutdown(1);
        }
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        // Sign-out or shutdown: WPF closes the windows next, and the main window must not hide itself instead.
        IsExiting = true;
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ShutdownAbpApplication();
        Log.CloseAndFlush();
#if (EnableTrayIcon)
        SingleInstance.Release();
#endif

        base.OnExit(e);
    }

    /// <summary>
    /// Stops the ABP host (Hangfire server, dashboard, disposables) before the process goes away.
    /// </summary>
    private void ShutdownAbpApplication()
    {
        var application = Interlocked.Exchange(ref _abpApplication, null);
        if (application is null)
        {
            return;
        }

        try
        {
            // Off the dispatcher: OnExit cannot await, and blocking the UI thread here would deadlock
            // any shutdown step that marshals back to it.
            if (!Task.Run(application.ShutdownAsync).Wait(ShutdownTimeout))
            {
                Log.Warning("ABP shutdown did not finish within {Timeout}; exiting anyway.", ShutdownTimeout);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ABP shutdown failed.");
        }
    }

    private void ExceptionHandler(ExceptionHandler handler)
    {
        DispatcherUnhandledException += handler.ApplicationExceptionHandler;
        TaskScheduler.UnobservedTaskException += handler.UnobservedTaskExceptionHandler;
        AppDomain.CurrentDomain.UnhandledException += handler.DomainExceptionHandler;
    }
}