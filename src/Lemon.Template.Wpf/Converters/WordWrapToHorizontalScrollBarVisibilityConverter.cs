using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;

namespace Lemon.Template.Wpf.Converters;

/// <summary>When word wrap is on, horizontal scroll is not needed; otherwise show horizontal bar for long lines.</summary>
public class WordWrapToHorizontalScrollBarVisibilityConverter : MarkupExtension, IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ScrollBarVisibility.Disabled;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}
