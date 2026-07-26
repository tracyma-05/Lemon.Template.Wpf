#if (EnableTrayIcon)
using H.NotifyIcon;
using H.NotifyIcon.Core;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
#endif
#if (EnableDesktopShortcut)
using Lemon.Template.Wpf.Infrastructures.Shell;
#endif
using Lemon.Template.Wpf.Infrastructures;
using Lemon.Template.Wpf.Infrastructures.Attributes;
using Lemon.Template.Wpf.Infrastructures.Exceptions;
using Lemon.Template.Wpf.Infrastructures.Localization;
using Lemon.Template.Wpf.Services.Localization;
using Lemon.Template.Wpf.Services.Theming;
using Lemon.Template.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;
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
#if (EnableTrayIcon)
    private TaskbarIcon? _taskbarIcon;
#endif

    private static IServiceProvider? _serviceProvider;

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
#if (EnableTrayIcon)
            InitializeTrayIcon(mainWindow);
#endif
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

    protected override void OnExit(ExitEventArgs e)
    {
#if (EnableTrayIcon)
        DisposeTrayIcon();
#endif
        ShutdownAbpApplication();
        Log.CloseAndFlush();

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

#if (EnableTrayIcon)
    private void DisposeTrayIcon()
    {
        _taskbarIcon?.Dispose();
        _taskbarIcon = null;
    }

    private void InitializeTrayIcon(MainWindow mainWindow)
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(App).Assembly;
        var title = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title
                    ?? assembly.GetName().Name
                    ?? "Application";

        var contextMenu = new ContextMenu();
        var exitItem = new MenuItem { Header = "Exit" };
        // Shutdown() runs OnExit (ABP shutdown, Hangfire stop, Serilog flush); Environment.Exit skips all of it.
        exitItem.Click += (_, _) => Shutdown();
        contextMenu.Items.Add(exitItem);

        ImageSource? iconSource = null;
        try
        {
            iconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/Images/logo.ico", UriKind.Absolute));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load tray icon resource.");
        }

        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = title,
            IconSource = iconSource,
            ContextMenu = contextMenu,
            MenuActivation = PopupActivationMode.RightClick,
            NoLeftClickDelay = true,
        };

        _taskbarIcon.TrayLeftMouseDown += (_, _) => BringMainWindowToFront(mainWindow);

        _taskbarIcon.ForceCreate();
    }

    private static void BringMainWindowToFront(MainWindow mainWindow)
    {
        var dispatcher = mainWindow.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            ActivateMainWindow(mainWindow);
        }
        else
        {
            _ = dispatcher.InvokeAsync(() => ActivateMainWindow(mainWindow), DispatcherPriority.Normal);
        }
    }

    private static void ActivateMainWindow(MainWindow mainWindow)
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

        _ = mainWindow.Activate();
    }
#endif

    private void ExceptionHandler(ExceptionHandler handler)
    {
        DispatcherUnhandledException += handler.ApplicationExceptionHandler;
        TaskScheduler.UnobservedTaskException += handler.UnobservedTaskExceptionHandler;
        AppDomain.CurrentDomain.UnhandledException += handler.DomainExceptionHandler;
    }
}