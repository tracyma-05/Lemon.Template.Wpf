using Lemon.Template.Wpf.Commons;
using Lemon.Template.Wpf.Models;
using Serilog;
using System.Linq;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Wpf.Infrastructures.Navigations
{
    public class MenuNavigator : IMenuNavigator, ISingletonDependency
    {
        private readonly INavigationService _navigationService;

        public MenuNavigator(INavigationService navigationService)
        {
            _navigationService = navigationService;
        }

        public bool NavigateTo(string registerGroup)
        {
            var page = FindMenuItem(registerGroup);
            if (page is null)
            {
                Log.Warning("Menu entry '{RegisterGroup}' is not registered; navigation was skipped.", registerGroup);
                return false;
            }

            SelectOnly(page);
            _navigationService.Navigate(page.Title);
            return true;
        }

        /// <summary>
        /// Walks the whole menu instead of relying on the <c>GroupName="Menu"</c> radio buttons to unselect
        /// each other: entries inside a collapsed expander may have no realized radio button to react.
        /// The group that owns the target page is expanded as well, otherwise a jump from the Home
        /// shortcuts would land on a page whose menu entry is still hidden inside a closed expander.
        /// </summary>
        private static void SelectOnly(NavigationItem page)
        {
            foreach (var root in Constants.NavigationItems)
            {
                root.IsSelected = ReferenceEquals(root, page);

                var ownsPage = false;
                foreach (var child in root.Items)
                {
                    var isTarget = ReferenceEquals(child, page);
                    child.IsSelected = isTarget;
                    ownsPage |= isTarget;
                }

                if (ownsPage)
                {
                    root.IsExpanded = true;
                }
            }
        }

        private static NavigationItem? FindMenuItem(string registerGroup)
        {
            if (string.IsNullOrWhiteSpace(registerGroup))
            {
                return null;
            }

            var parts = registerGroup.Split('/');
            return parts.Length switch
            {
                // A top-level page is its own menu entry, so it is the one root that carries no children.
                1 => Constants.NavigationItems
                    .FirstOrDefault(root => root.Title == parts[0] && root.Items.Count == 0),
                2 => Constants.NavigationItems
                    .FirstOrDefault(root => root.Title == parts[0])?
                    .Items.FirstOrDefault(child => child.Title == parts[1]),
                _ => null,
            };
        }
    }
}
