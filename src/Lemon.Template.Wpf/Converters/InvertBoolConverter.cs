using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace Lemon.Template.Wpf.Converters;

public class InvertBoolConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b && !b;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}