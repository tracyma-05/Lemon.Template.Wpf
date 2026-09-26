using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Template.Avalonia.Commons;
using Lemon.Template.Avalonia.Infrastructures.Dialogs;
using Lemon.Template.Avalonia.Infrastructures.Navigations;
using Lemon.Template.Avalonia.Infrastructures.Shell;
using Lemon.Template.Avalonia.Models;
using Lemon.Template.Avalonia.Services.Theming;
using Lemon.Template.Avalonia.Services.Updates;
using Lemon.Template.Avalonia.ViewModels.Dialogs;
using Serilog;
using System.Collections.ObjectModel;
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
        private bool _muteIsDarkThemeCallback;

        public MainWindowViewModel(
            INavigationService navigationService,
            IMenuNavigator menuNavigator,
            IAppThemeService appThemeService,
            IUpdateService updateService,
            IHostDialogService dialogService)
        {
            _navigationService = navigationService;
            _menuNavigator = menuNavigator;
            _appThemeService = appThemeService;
            _updateService = updateService;
            _dialogService = dialogService;
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

            // The browser downloads the package; on macOS the user then moves the .app out of the zip.
            if (dialogResult.Result == ButtonResult.OK && result.DownloadUrl is not null)
            {
                BrowserLauncher.TryOpen(result.DownloadUrl.AbsoluteUri);
            }
        }

        #endregion
    }
}
