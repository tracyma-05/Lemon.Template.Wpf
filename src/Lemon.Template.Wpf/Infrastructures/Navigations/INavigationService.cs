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

        /// <summary>
        /// Detaches a view from its region (removing its tab when hosted in a
        /// <see cref="Themes.Controls.TabControl"/>) and releases the ViewModel it was wired with.
        /// </summary>
        void RemoveView(UserControl view);
    }
}