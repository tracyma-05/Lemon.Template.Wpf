using System.IO;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Lemon.Template.Wpf.Infrastructures.Data;
using Lemon.Template.Wpf.Infrastructures.Localization;
using MaterialDesignThemes.Wpf;
using Serilog;

namespace Lemon.Template.Wpf.Infrastructures.Shell;

/// <summary>
/// Tray icon for close-to-tray: double-click or "Open" shows the main window, "Exit" quits.
/// Uses H.NotifyIcon.Wpf rather than WinForms' NotifyIcon, so the menu is a plain WPF <see cref="ContextMenu"/>
/// that follows the Material Design theme and switches language live.
/// </summary>
internal sealed class AppTrayIcon : IDisposable
{
    // Marker file: the "still running in the tray" hint is shown once per user, not on every close.
    private static readonly string HintShownMarker = Path.Combine(AppSqlitePaths.ApplicationDataFolder, "tray-hint-shown");

    private TaskbarIcon? _taskbarIcon;

    public AppTrayIcon(Action open, Action exit)
    {
        var contextMenu = new ContextMenu();
        contextMenu.Items.Add(CreateMenuItem("Tray_Open", PackIconKind.WindowRestore, open));
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(CreateMenuItem("Tray_Exit", PackIconKind.Power, exit));

        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = ApplicationTitle,
            IconSource = LoadIcon(),
            ContextMenu = contextMenu,
            MenuActivation = PopupActivationMode.RightClick,
        };

        // Double-click only: a single click in the notification area is too easy to hit by accident.
        _taskbarIcon.TrayMouseDoubleClick += (_, _) => open();

        _taskbarIcon.ForceCreate();
    }

    /// <summary>
    /// Tells the user, once, that closing hid the window instead of quitting. Without it the window and the
    /// taskbar button disappear together and the app looks like it crashed.
    /// </summary>
    public void ShowHiddenHintOnce()
    {
        if (_taskbarIcon is null || File.Exists(HintShownMarker))
        {
            return;
        }

        try
        {
            var localization = LocalizationService.Instance;
            _taskbarIcon.ShowNotification(ApplicationTitle, localization.GetString("Tray_HiddenHint"), NotificationIcon.Info);

            Directory.CreateDirectory(Path.GetDirectoryName(HintShownMarker)!);
            File.WriteAllBytes(HintShownMarker, []);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not show the tray hint.");
        }
    }

    public void Dispose()
    {
        // Without this the icon stays in the tray after the process has gone, until the mouse passes over it.
        var taskbarIcon = Interlocked.Exchange(ref _taskbarIcon, null);
        if (taskbarIcon is null)
        {
            return;
        }

        taskbarIcon.Visibility = System.Windows.Visibility.Collapsed;
        taskbarIcon.Dispose();
    }

    private static MenuItem CreateMenuItem(string textKey, PackIconKind icon, Action onClick)
    {
        var item = new MenuItem { Icon = new PackIcon { Kind = icon } };
        // Bound, not assigned: switching language in Settings updates the menu without a restart.
        item.SetBinding(HeaderedItemsControl.HeaderProperty, new LocalizeExtension(textKey));
        item.Click += (_, _) => onClick();
        return item;
    }

    private static string ApplicationTitle
    {
        get
        {
            var assembly = Assembly.GetEntryAssembly() ?? typeof(AppTrayIcon).Assembly;
            return assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title
                   ?? assembly.GetName().Name
                   ?? "Application";
        }
    }

    private static ImageSource? LoadIcon()
    {
        try
        {
            return new BitmapImage(new Uri("pack://application:,,,/Assets/Images/logo.ico", UriKind.Absolute));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load tray icon resource.");
            return null;
        }
    }
}
