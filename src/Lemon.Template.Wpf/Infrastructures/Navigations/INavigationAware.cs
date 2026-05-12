namespace Lemon.Template.Wpf.Infrastructures.Navigations
{
    /// <summary>
    /// View or ViewModel hooks for region-style navigation (Prism <c>IRegionAware</c> / <c>INavigationAware</c> pattern).
    /// </summary>
    public interface INavigationAware
    {
        /// <summary>
        /// Called after the view is active in the region (host updated). <paramref name="navigationContext"/> describes this navigation.
        /// </summary>
        void OnNavigatedTo(NavigationContext navigationContext);

        /// <summary>
        /// Called before the region navigates away. <paramref name="navigationContext"/> describes the target navigation.
        /// </summary>
        void OnNavigatedFrom(NavigationContext navigationContext);
    }
}