using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace Lemon.Template.Wpf.Converters
{
    public class BoolToAngleConverter : MarkupExtension, IValueConverter
    {
        public double TrueAngle { get; set; } = 90.0;
        public double FalseAngle { get; set; } = 0.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b) return TrueAngle;
            return FalseAngle;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider) => this;
    }
}