#if (EnableTrayIcon)
using Avalonia.Platform;
#endif
#if (EnableDesktopShortcut)
using Lemon.Template.Avalonia.Infrastructures.Shell;
#endif
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Lemon.Template.Avalonia.Infrastructures;
using Lemon.Template.Avalonia.Infrastructures.Attributes;
using Lemon.Template.Avalonia.Infrastructures.Data;
using Lemon.Template.Avalonia.Infrastructures.Exceptions;
using Lemon.Template.Avalonia.Infrastructures.Localization;
using Lemon.Template.Avalonia.Services.Localization;
using Lemon.Template.Avalonia.Services.Theming;
using Lemon.Template.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using System.IO;
using System.Reflection;
using Volo.Abp;

namespace Lemon.Template.Avalonia;

public partial class App : Application
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(10);

    private IAbpApplicationWithInternalServiceProvider? _abpApplication;
#if (EnableTrayIcon)
    private TrayIcon? _trayIcon;
#endif

    private static IServiceProvider? _serviceProvider;

    /// <summary>
    /// Ambient container for the few Avalonia extension points that cannot take constructor injection
    /// (attached-property callbacks, class handlers). Use constructor injection everywhere else.
    /// </summary>
    internal static IServiceProvider ServiceProvider =>
        _serviceProvider ?? throw new InvalidOperationException(
            "Application services are not available yet: the ABP host has not finished initializing.");

    /// <summary>
    /// Non-throwing counterpart of <see cref="ServiceProvider"/> for callers that legitimately run
    /// before the host is ready — the global Loaded class handler fires for the splash screen too.
    /// </summary>
    internal static IServiceProvider? ServiceProviderOrNull => _serviceProvider;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ConfigureLogging();
            RegisterExceptionHandlers(new ExceptionHandler());

            // Explicit until the main window is up: the splash is closed during the hand-over, and the
            // default (last window closed) would end the process right there.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => OnExit();

            var splash = new SplashWindow();
            desktop.MainWindow = splash;

            // Posted so the splash gets painted before the host start-up work begins.
            Dispatcher.UIThread.Post(() => _ = StartAsync(desktop, splash), DispatcherPriority.Background);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureLogging()
    {
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
                // Per-user data folder, not the app folder: a macOS .app bundle is not writable, and the
                // Logs → Local-Logs page reads from the same place.
                path: Path.Combine(AppSqlitePaths.LogsFolder, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                shared: true,
                encoding: System.Text.Encoding.UTF8))
            .CreateLogger();
    }

    /// <remarks>Every failure is handled here, so the caller can fire and forget it.</remarks>
    private async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop, SplashWindow splash)
    {
        try
        {
            Log.Information("Starting Avalonia host.");

#if (EnableDesktopShortcut)
            // .lnk shortcuts are a Windows concept; there is no equivalent to create on macOS.
            if (OperatingSystem.IsWindows())
            {
                DesktopShortcutHelper.EnsureDesktopShortcut();
            }
#endif

            _abpApplication = await AbpApplicationFactory.CreateAsync<AppModule>(options =>
            {
                options.UseAutofac();
                options.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));
            });

            await _abpApplication.InitializeAsync();
            var services = _abpApplication.ServiceProvider;
            _serviceProvider = services;

            // Only now that the container exists: the handler is global, so registering it any earlier
            // means every control loaded during startup asks for services that are not there yet.
            ViewModelLocator.EnableAutoWiring();

            ServiceCollectionKeyedExtensions.AddRouteServiceFromAssembly(services, typeof(App).Assembly);

            // Before any view is created, so the first render already uses the chosen language.
            var languageStore = services.GetRequiredService<IAppLanguagePreferencesStore>();
            LocalizationService.Instance.SetCulture(
                LocalizationService.Instance.ResolveSupportedCulture(languageStore.Load()));

            var themeStore = services.GetRequiredService<IAppThemePreferencesStore>();
            var themeService = services.GetRequiredService<IAppThemeService>();
            var snapshot = themeStore.Load();
            if (snapshot is not null)
            {
                themeService.ApplySnapshot(snapshot);
            }

            var mainWindow = services.GetRequiredService<MainWindow>();
            mainWindow.Opened += (_, _) => splash.Close();

            desktop.MainWindow = mainWindow;
            mainWindow.Show();
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

#if (EnableTrayIcon)
            InitializeTrayIcon(desktop, mainWindow);
#endif
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly!");
            splash.Close();

            await ErrorWindow.ShowAsync(
                $"The application failed to start:{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "Startup failed",
                isWarning: false);

            // Without this the process would linger with no window and no way to quit.
            desktop.Shutdown(1);
        }
    }

    private void OnExit()
    {
#if (EnableTrayIcon)
        DisposeTrayIcon();
#endif
        ShutdownAbpApplication();
        Log.CloseAndFlush();
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
            // Off the dispatcher: Exit cannot await, and blocking the UI thread here would deadlock
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

#if (EnableTrayIcon)
    private void DisposeTrayIcon()
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    /// <summary>
    /// Built in code rather than in App.axaml so the whole feature stays inside one template switch.
    /// On Windows this is the notification-area icon; on macOS it is a menu bar extra.
    /// </summary>
    private void InitializeTrayIcon(IClassicDesktopStyleApplicationLifetime desktop, MainWindow mainWindow)
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(App).Assembly;
        var title = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title
                    ?? assembly.GetName().Name
                    ?? "Application";

        var exitItem = new NativeMenuItem("Exit");
        // Shutdown() raises Exit (ABP shutdown, Hangfire stop, Serilog flush); Environment.Exit skips all of it.
        exitItem.Click += (_, _) => desktop.Shutdown();

        WindowIcon? icon = null;
        try
        {
            // .ico is what the Windows tray expects; macOS decodes the .png reliably.
            var iconFile = OperatingSystem.IsWindows() ? "logo.ico" : "logo.png";
            using var stream = AssetLoader.Open(new Uri($"avares://Lemon.Template.Avalonia/Assets/Images/{iconFile}"));
            icon = new WindowIcon(stream);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load tray icon resource.");
        }

        _trayIcon = new TrayIcon
        {
            ToolTipText = title,
            Icon = icon,
            Menu = new NativeMenu { exitItem },
        };

        // Windows raises Clicked on a left click; macOS opens the menu instead and never raises it.
        _trayIcon.Clicked += (_, _) => BringMainWindowToFront(mainWindow);

        TrayIcon.SetIcons(this, new TrayIcons { _trayIcon });
    }

    private static void BringMainWindowToFront(Window mainWindow)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ActivateMainWindow(mainWindow);
        }
        else
        {
            Dispatcher.UIThread.Post(() => ActivateMainWindow(mainWindow));
        }
    }

    private static void ActivateMainWindow(Window mainWindow)
    {
        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        mainWindow.Show();

        // Brief Topmost toggle helps foreground when Activate() alone is ignored.
        var wasTopmost = mainWindow.Topmost;
        mainWindow.Topmost = true;
        mainWindow.Topmost = wasTopmost;

        mainWindow.Activate();
    }
#endif

    private static void RegisterExceptionHandlers(ExceptionHandler handler)
    {
        Dispatcher.UIThread.UnhandledException += handler.ApplicationExceptionHandler;
        TaskScheduler.UnobservedTaskException += handler.UnobservedTaskExceptionHandler;
        AppDomain.CurrentDomain.UnhandledException += handler.DomainExceptionHandler;
    }
}
