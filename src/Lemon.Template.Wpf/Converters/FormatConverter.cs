using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace Lemon.Template.Wpf.Converters
{
    /// <summary>
    /// Localized text with values in it: the first binding is the format string (a <c>loc:Localize</c> binding, so
    /// it follows the interface language), the rest fill its <c>{0}</c>, <c>{1}</c>…
    /// <code>
    /// &lt;MultiBinding Converter="{converters:FormatConverter}"&gt;
    ///     &lt;loc:LocalizeExtension Key="LocalLog_Loaded" /&gt;
    ///     &lt;Binding Path="FilePath" /&gt;
    ///     &lt;Binding Path="SizeKb" /&gt;
    /// &lt;/MultiBinding&gt;
    /// </code>
    /// A plain <c>StringFormat</c> cannot do this: it is fixed when the XAML loads.
    /// </summary>
    public class FormatConverter : MarkupExtension, IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 0 || values[0] is not string format)
            {
                return string.Empty;
            }

            var args = values.Skip(1).Select(x => x == DependencyProperty.UnsetValue ? null : x).ToArray();
            return string.Format(culture, format, args);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider) => this;
    }
}
