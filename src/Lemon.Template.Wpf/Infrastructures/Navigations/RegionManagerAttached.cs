using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace Lemon.Template.Wpf.Infrastructures.Navigations
{
    public static class RegionManagerAttached
    {
        public static readonly DependencyProperty RegionNameProperty =
            DependencyProperty.RegisterAttached(
                "RegionName",
                typeof(string),
                typeof(RegionManagerAttached),
                new PropertyMetadata(null, OnRegionNameChanged));

        public static void SetRegionName(DependencyObject element, string value)
        {
            element.SetValue(RegionNameProperty, value);
        }

        public static string GetRegionName(DependencyObject element)
        {
            return (string)element.GetValue(RegionNameProperty);
        }

        private static void OnRegionNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Control region && e.NewValue is string regionName)
            {
                var regionManager = App.ServiceProvider.GetRequiredService<INavigationService>();
                regionManager.RegisterRegion(regionName, region);
            }
        }
    }
}