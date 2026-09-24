using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Lemon.Template.Avalonia.Models;
using Lemon.Template.Avalonia.Themes.Controls;
using System.Globalization;

namespace Lemon.Template.Avalonia.Converters
{
    public class MenuTitleConverter : MarkupExtension, IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TabCloseItem tabItem)
            {
                if (tabItem.Content is UserControl { DataContext: NavigationViewModel viewModel })
                {
                    tabItem.Header = viewModel.Title;
                }

                return tabItem.Header;
            }
            return string.Empty;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider) => this;
    }
}
