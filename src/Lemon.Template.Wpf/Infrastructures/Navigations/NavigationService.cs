using Lemon.Template.Wpf.Infrastructures;
using Lemon.Template.Wpf.Themes.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Wpf.Infrastructures.Navigations
{
    public class NavigationService : INavigationService, ISingletonDependency
    {
        private readonly IServiceProvider _serviceProvider;

        private readonly Dictionary<string, Control> _regions = new();
        private readonly Dictionary<string, UserControl> _currentViews = new();
        private readonly Dictionary<string, (Type ViewType, string RegionName)> _routes = new();
        private readonly Dictionary<string, TabCloseItem> _tabItems = new();

        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void RegisterRegion(string regionName, Control region)
        {
            _regions[regionName] = region;
        }

        public void Navigate(string routeName, NavigationParameters? parameters = null)
        {
            if (string.IsNullOrEmpty(routeName))
                throw new ArgumentException("Route name cannot be null or empty.", nameof(routeName));

            if (!_routes.TryGetValue(routeName, out var routeInfo))
                throw new InvalidOperationException($"Route '{routeName}' not found.");

            NavigateInternal(routeName, routeInfo.RegionName, parameters);
        }

        public void RegisterRoute(string routeName, Type viewType, string regionName)
        {
            if (!typeof(UserControl).IsAssignableFrom(viewType))
                throw new ArgumentException($"Route type must be a UserControl: {viewType.Name}");

            _routes[routeName] = (viewType, regionName);
        }

        public void RemoveView(UserControl view)
        {
            ArgumentNullException.ThrowIfNull(view);

            var tabKeys = _tabItems
                .Where(entry => ReferenceEquals(entry.Value.Content, view))
                .Select(entry => entry.Key)
                .ToList();

            foreach (var key in tabKeys)
            {
                var tabItem = _tabItems[key];
                _tabItems.Remove(key);

                foreach (var tabControl in _regions.Values.OfType<Themes.Controls.TabControl>())
                {
                    if (tabControl.Items.Contains(tabItem))
                        tabControl.Items.Remove(tabItem);
                }

                tabItem.Content = null;
            }

            var regionNames = _currentViews
                .Where(entry => ReferenceEquals(entry.Value, view))
                .Select(entry => entry.Key)
                .ToList();

            foreach (var regionName in regionNames)
            {
                _currentViews.Remove(regionName);

                if (_regions.TryGetValue(regionName, out var region) &&
                    region is ContentControl contentControl &&
                    ReferenceEquals(contentControl.Content, view))
                {
                    contentControl.Content = null;
                }
            }

            ViewModelLocator.ReleaseViewModel(view);
        }

        private void NavigateInternal(string routeName, string regionName, NavigationParameters? parameters)
        {
            if (!_regions.TryGetValue(regionName, out var region))
                throw new InvalidOperationException($"Region '{regionName}' is not registered.");

            var incomingContext = new NavigationContext(routeName, regionName, parameters ?? new NavigationParameters());

            if (_currentViews.TryGetValue(regionName, out var previousView))
            {
                ViewModelLocator.ViewAndViewModelAction<INavigationAware>(
                    previousView,
                    n => n.OnNavigatedFrom(incomingContext));
            }

            var view = ResolveView(region, routeName, regionName);
            ViewModelLocator.AutoWireViewModel(view, _serviceProvider);

            switch (region)
            {
                case ContentControl contentControl:
                    contentControl.Content = view;

                    // A ContentControl region keeps only one view, so the replaced one is gone for good:
                    // release its ViewModel scope here or it leaks. Tab regions cache views and must not.
                    if (previousView != null && !ReferenceEquals(previousView, view))
                    {
                        ViewModelLocator.ReleaseViewModel(previousView);
                    }
                    break;

                case Themes.Controls.TabControl tabControl:
                    var key = $"{routeName}.{regionName}";
                    if (!_tabItems.TryGetValue(key, out var tabItem) || !tabControl.Items.Contains(tabItem))
                    {
                        _tabItems.Remove(key);
                        tabItem = new TabCloseItem
                        {
                            Header = routeName,
                            Content = view
                        };
                        tabControl.Items.Add(tabItem);
                        _tabItems[key] = tabItem;
                    }
                    else if (!ReferenceEquals(tabItem.Content, view))
                    {
                        tabItem.Content = view;
                    }

                    tabControl.SelectedItem = tabItem;
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported region type: {region.GetType().Name}. " +
                        "Only ContentControl or TabControl are supported.");
            }

            ViewModelLocator.ViewAndViewModelAction<INavigationAware>(
                view,
                n => n.OnNavigatedTo(incomingContext));

            _currentViews[regionName] = view;
        }

        private UserControl ResolveView(Control region, string routeName, string regionName)
        {
            var key = $"{routeName}.{regionName}";

            if (region is Themes.Controls.TabControl tabControl)
            {
                if (_tabItems.TryGetValue(key, out var existingTab))
                {
                    if (tabControl.Items.Contains(existingTab) && existingTab.Content is UserControl existingView)
                        return existingView;

                    _tabItems.Remove(key);
                }
            }

            return _serviceProvider.GetRequiredKeyedService<UserControl>(key);
        }
    }
}
