using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Template.Avalonia.Commons;
using Lemon.Template.Avalonia.Infrastructures.Dialogs;
using Lemon.Template.Avalonia.Infrastructures.Localization;
using Lemon.Template.Avalonia.Infrastructures.Navigations;
using Lemon.Template.Avalonia.Infrastructures.Shell;
using Lemon.Template.Avalonia.Models;
using Lemon.Template.Avalonia.Services.Shortcuts;
using Lemon.Template.Avalonia.Services.Theming;
using Lemon.Template.Avalonia.Services.Updates;
using Lemon.Template.Avalonia.ViewModels.Dialogs;
using Serilog;
using System.Collections.ObjectModel;
using System.IO;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Avalonia.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject, ISingletonDependency
    {
        private readonly INavigationService _navigationService;
        private readonly IMenuNavigator _menuNavigator;
        private readonly IAppThemeService _appThemeService;
        private readonly IUpdateService _updateService;
        private readonly IHostDialogService _dialogService;
        private readonly IUpdateInstaller _updateInstaller;
        private bool _muteIsDarkThemeCallback;

        public MainWindowViewModel(
            INavigationService navigationService,
            IMenuNavigator menuNavigator,
            IAppThemeService appThemeService,
            IUpdateService updateService,
            IHostDialogService dialogService,
            IUpdateInstaller updateInstaller)
        {
            _navigationService = navigationService;
            _menuNavigator = menuNavigator;
            _appThemeService = appThemeService;
            _updateService = updateService;
            _dialogService = dialogService;
            _updateInstaller = updateInstaller;
            _appThemeService.DarkThemeChanged += OnAppDarkThemeChanged;

            NavigationItems = Constants.NavigationItems;

            IsDarkTheme = _appThemeService.IsDarkTheme();

            // Start-up page. A missing entry only logs, so the shell still opens.
            _menuNavigator.NavigateTo(Constants.Home);
        }

        [ObservableProperty]
        private ObservableCollection<NavigationItem> _navigationItems;

        [ObservableProperty]
        private bool _isDarkTheme;

        partial void OnIsDarkThemeChanged(bool value)
        {
            if (_muteIsDarkThemeCallback)
            {
                return;
            }

            _appThemeService.SetDarkTheme(value);
        }

        private void OnAppDarkThemeChanged(object? sender, bool isDark)
        {
            if (IsDarkTheme == isDark)
            {
                return;
            }

            _muteIsDarkThemeCallback = true;
            try
            {
                IsDarkTheme = isDark;
            }
            finally
            {
                _muteIsDarkThemeCallback = false;
            }
        }

        /// <summary>Title bar light / dark button. A plain button, not a ToggleButton: the icon shows the state.</summary>
        [RelayCommand]
        private void ToggleDarkTheme() => IsDarkTheme = !IsDarkTheme;

        [RelayCommand]
        private void Navigate(NavigationItem item)
        {
            _navigationService.Navigate(item.Title);
        }

        #region desktop shortcut

        /// <summary>Windows, and an app started from its own executable; see <see cref="DesktopShortcut.IsSupported"/>.</summary>
        public bool IsDesktopShortcutSupported => DesktopShortcut.IsSupported;

        /// <summary>
        /// Asks first (naming the file, and that one of the same name is replaced), then puts a shortcut to this
        /// executable on the desktop. It survives automatic updates, which replace the folder's files in place.
        /// </summary>
        [RelayCommand(AllowConcurrentExecutions = false)]
        private async Task CreateDesktopShortcutAsync()
        {
            var localization = LocalizationService.Instance;
            var name = localization.GetString("App_DisplayName");
            if (!await _dialogService.Question(
                    title: localization.GetString("Shortcut_Title"),
                    message: localization.Format("Shortcut_Confirm", name, DesktopShortcut.PathFor(name))))
            {
                return;
            }

            try
            {
                var path = DesktopShortcut.Create(name, name);
                Log.Information("Created the desktop shortcut {Path}.", path);
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or IOException
                                           or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                Log.Warning(ex, "Could not create the desktop shortcut.");
                await _dialogService.Question(
                    title: localization.GetString("Shortcut_Title"),
                    message: localization.Format("Shortcut_Failed", ex.Message));
            }
        }

        #endregion

        #region updates

        /// <summary>
        /// Whether the title bar shows the check-for-updates button: only when <c>Update:Enabled</c> is
        /// on and an update source is configured. Decided once at start-up, like the rest of the shell.
        /// </summary>
        public bool IsUpdateCheckEnabled => _updateService.IsEnabled;

        /// <summary>A newer version was found; the button shows a badge until the user is up to date.</summary>
        [ObservableProperty]
        private bool _isUpdateAvailable;

        [ObservableProperty]
        private bool _isCheckingForUpdates;

        [RelayCommand(AllowConcurrentExecutions = false)]
        private async Task CheckForUpdatesAsync()
        {
            var result = await RunUpdateCheckAsync();
            await ShowUpdateResultAsync(result);
        }

        /// <summary>
        /// Quiet check once the main window is on screen: nothing is shown unless a newer version exists,
        /// so an offline start or an unreachable server does not greet the user with an error.
        /// </summary>
        public async Task CheckForUpdatesOnStartupAsync()
        {
            if (!_updateService.CheckOnStartup)
            {
                return;
            }

            try
            {
                var result = await RunUpdateCheckAsync();
                if (result.Status == UpdateCheckStatus.UpdateAvailable)
                {
                    await ShowUpdateResultAsync(result);
                }
            }
            catch (Exception ex)
            {
                // Called from an async void event handler: never let this take the window down.
                Log.Warning(ex, "Start-up update check failed.");
            }
        }

        private async Task<UpdateCheckResult> RunUpdateCheckAsync()
        {
            IsCheckingForUpdates = true;
            try
            {
                var result = await _updateService.CheckAsync();

                // A failed check says nothing about whether an update exists, so keep the last answer.
                if (result.Status != UpdateCheckStatus.Failed)
                {
                    IsUpdateAvailable = result.Status == UpdateCheckStatus.UpdateAvailable;
                }

                return result;
            }
            finally
            {
                IsCheckingForUpdates = false;
            }
        }

        private async Task ShowUpdateResultAsync(UpdateCheckResult result)
        {
            var parameters = new DialogParameters { { UpdateDialogViewModel.ResultParameter, result } };
            var dialogResult = await _dialogService.ShowDialogAsync(Constants.UpdateDialog, parameters);

            if (dialogResult.Result != ButtonResult.OK)
            {
                return;
            }

            if (dialogResult.Parameters.ContainsKey(UpdateDialogViewModel.PreparedParameter))
            {
                InstallAndRestart(dialogResult.Parameters.GetValue<PreparedUpdate>(UpdateDialogViewModel.PreparedParameter));
            }
            else if (result.DownloadUrl is not null)
            {
                // The browser downloads the package; on macOS the user then moves the .app out of the zip.
                BrowserLauncher.TryOpen(result.DownloadUrl.AbsoluteUri);
            }
        }

        /// <summary>
        /// Starts the new version's updater and exits: the updater waits for this process to end before it
        /// swaps the files, then starts the new version. Shutdown runs the Exit handlers (ABP shutdown,
        /// Serilog flush) and, being an application shutdown, skips the exit confirmation.
        /// </summary>
        private void InstallAndRestart(PreparedUpdate update)
        {
            try
            {
                _updateInstaller.Launch(update);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not start the updater for {Version}.", update.Version);
                return;
            }

            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        }

        #endregion
    }
}
