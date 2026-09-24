using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace Lemon.Template.Avalonia.Infrastructures.Navigations
{
    public static class RegionManagerAttached
    {
        public static readonly AttachedProperty<string?> RegionNameProperty =
            AvaloniaProperty.RegisterAttached<Control, string?>("RegionName", typeof(RegionManagerAttached));

        static RegionManagerAttached()
        {
            RegionNameProperty.Changed.AddClassHandler<Control>(OnRegionNameChanged);
        }

        public static void SetRegionName(Control element, string? value)
        {
            element.SetValue(RegionNameProperty, value);
        }

        public static string? GetRegionName(Control element)
        {
            return element.GetValue(RegionNameProperty);
        }

        private static void OnRegionNameChanged(Control region, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is not string regionName)
            {
                return;
            }

            // XAML 预览器会在没有 ABP 宿主的情况下实例化视图，此时没有可注册的导航服务。
            if (Design.IsDesignMode)
            {
                return;
            }

            var regionManager = App.ServiceProvider.GetRequiredService<INavigationService>();
            regionManager.RegisterRegion(regionName, region);
        }
    }
}
