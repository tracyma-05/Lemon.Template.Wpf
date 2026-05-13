using H.NotifyIcon;
using H.NotifyIcon.Core;
using Lemon.Template.Wpf.Infrastructures.Attributes;
using Lemon.Template.Wpf.Infrastructures.Exceptions;
using Lemon.Template.Wpf.Infrastructures.Shell;
using Lemon.Template.Wpf.Services.Theming;
using Lemon.Template.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Volo.Abp;

namespace Lemon.Template.Wpf;

public partial class App : Application
{
    private IAbpApplicationWithInternalServiceProvider? _abpApplication;
    private TaskbarIcon? _taskbarIcon;

    internal static IServiceProvider ServiceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Async(c => c.File(
                path: Path.Combine("Logs", "log-.txt"),
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

            DesktopShortcutHelper.EnsureDesktopShortcut();

            _abpApplication = await AbpApplicationFactory.CreateAsync<WpfModule>(options =>
            {
                options.UseAutofac();
                options.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));
            });

            await _abpApplication.InitializeAsync();
            ServiceProvider = _abpApplication.ServiceProvider;

            ServiceCollectionKeyedExtensions.AddRouteServiceFromAssembly(ServiceProvider, typeof(App).Assembly);

            await Dispatcher.InvokeAsync(() =>
            {
                var themeStore = ServiceProvider.GetRequiredService<IAppThemePreferencesStore>();
                var themeService = ServiceProvider.GetRequiredService<IAppThemeService>();
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
            InitializeTrayIcon(mainWindow);
        }
        catch (Exception ex)
        {
            splash?.Close();
            Log.Fatal(ex, "Host terminated unexpectedly!");
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        DisposeTrayIcon();

        if (_abpApplication != null)
        {
            await _abpApplication.ShutdownAsync();
        }

        Log.CloseAndFlush();
    }

    private void DisposeTrayIcon()
    {
        _taskbarIcon?.Dispose();
        _taskbarIcon = null;
    }

    private void InitializeTrayIcon(MainWindow mainWindow)
    {
        var title = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "Pitaya.Work";

        var contextMenu = new ContextMenu();
        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => Environment.Exit(0);
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

    private void ExceptionHandler(ExceptionHandler handler)
    {
        DispatcherUnhandledException += handler.ApplicationExceptionHandler;
        TaskScheduler.UnobservedTaskException += handler.UnobservedTaskExceptionHandler;
        AppDomain.CurrentDomain.UnhandledException += handler.DomainExceptionHandler;
    }
}