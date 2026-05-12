using System;
using System.Windows.Controls;

namespace Lemon.Template.Wpf.Infrastructures.Navigations
{
    public interface INavigationService
    {
        void RegisterRegion(string regionName, Control region);
        void RegisterRoute(string routeName, Type viewType, string regionName);

        /// <param name="routeName">Must not be null or empty.</param>
        void Navigate(string routeName, NavigationParameters? parameters = null);
    }
}