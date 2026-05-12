using System;

namespace Lemon.Template.Wpf.Infrastructures.Navigations
{
    /// <summary>
    /// Describes an in-flight or completed navigation (similar in role to Prism's <c>NavigationContext</c>).
    /// </summary>
    public sealed class NavigationContext
    {
        public NavigationContext(string routeName, string regionName, NavigationParameters parameters)
        {
            RouteName = routeName ?? throw new ArgumentNullException(nameof(routeName));
            RegionName = regionName ?? throw new ArgumentNullException(nameof(regionName));
            Parameters = parameters ?? new NavigationParameters();
        }

        public string RouteName { get; }

        public string RegionName { get; }

        public NavigationParameters Parameters { get; }
    }
}
