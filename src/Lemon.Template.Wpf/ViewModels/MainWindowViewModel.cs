using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Template.Wpf.Commons;
using Lemon.Template.Wpf.Infrastructures.Navigations;
using Lemon.Template.Wpf.Models;
using Lemon.Template.Wpf.Services.Theming;
using System.Collections.ObjectModel;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Wpf.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject, ISingletonDependency
    {
        private readonly INavigationService _navigationService;
        private readonly IMenuNavigator _menuNavigator;
        private readonly IAppThemeService _appThemeService;
        private bool _muteIsDarkThemeCallback;

        public MainWindowViewModel(
            INavigationService navigationService,
            IMenuNavigator menuNavigator,
            IAppThemeService appThemeService)
        {
            _navigationService = navigationService;
            _menuNavigator = menuNavigator;
            _appThemeService = appThemeService;
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

        [RelayCommand]
        private void Navigate(NavigationItem item)
        {
            _navigationService.Navigate(item.Title);
        }
    }
}
