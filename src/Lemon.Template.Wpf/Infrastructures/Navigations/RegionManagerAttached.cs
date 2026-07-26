using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
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
            if (d is not Control region || e.NewValue is not string regionName)
            {
                return;
            }

            // XAML 设计器会在没有 ABP 宿主的情况下实例化视图，此时没有可注册的导航服务。
            if (DesignerProperties.GetIsInDesignMode(d))
            {
                return;
            }

            var regionManager = App.ServiceProvider.GetRequiredService<INavigationService>();
            regionManager.RegisterRegion(regionName, region);
        }
    }
}