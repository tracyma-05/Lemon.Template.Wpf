using Lemon.Template.Wpf.Models;
using Lemon.Template.Wpf.Themes.Controls;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;

namespace Lemon.Template.Wpf.Converters
{
    public class MenuTitleConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && value is TabCloseItem tabItem)
            {
                var ctor = tabItem.Content as UserControl;
                if (ctor != null && ctor.DataContext is NavigationViewModel viewModel)
                {
                    tabItem.Header = viewModel.Title;
                }

                return tabItem.Header;
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider) => this;
    }
}