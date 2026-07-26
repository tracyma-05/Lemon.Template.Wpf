namespace Lemon.Template.Wpf.Infrastructures.Navigations
{
    /// <summary>
    /// Navigates by menu registration key and keeps the side-navigation highlight in sync.
    /// </summary>
    /// <remarks>
    /// <see cref="INavigationService"/> only swaps the region content; anything that navigates from outside
    /// the menu itself (the start-up page, a shortcut on the home page) also has to move the selection, or
    /// the shell ends up showing one page while highlighting another.
    /// </remarks>
    public interface IMenuNavigator
    {
        /// <summary>
        /// Navigates to the page registered under <paramref name="registerGroup"/> (<c>Group/Name</c>, or a
        /// single <c>Name</c> for a top-level page) and selects its menu entry.
        /// </summary>
        /// <returns><c>false</c> when no menu entry matches; the failure is logged rather than thrown, so a
        /// page that was excluded by a template symbol cannot break the shell.</returns>
        bool NavigateTo(string registerGroup);
    }
}
