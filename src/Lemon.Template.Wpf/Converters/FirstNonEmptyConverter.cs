using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace Lemon.Template.Wpf.Converters
{
    /// <summary>
    /// The first value that is not null or empty: a bound value with a localized placeholder after it, which
    /// <c>TargetNullValue</c> cannot provide because it is fixed when the XAML loads.
    /// <code>
    /// &lt;MultiBinding Converter="{converters:FirstNonEmptyConverter}"&gt;
    ///     &lt;Binding Path="Version" /&gt;
    ///     &lt;loc:LocalizeExtension Key="Update_CurrentVersion" /&gt;
    /// &lt;/MultiBinding&gt;
    /// </code>
    /// </summary>
    public class FirstNonEmptyConverter : MarkupExtension, IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            foreach (var value in values)
            {
                if (value is not null && value != DependencyProperty.UnsetValue && value.ToString() is { Length: > 0 } text)
                {
                    return text;
                }
            }

            return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider) => this;
    }
}
