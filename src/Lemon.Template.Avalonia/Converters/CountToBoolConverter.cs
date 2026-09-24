using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using System.Globalization;

namespace Lemon.Template.Avalonia.Converters;

/// <summary>
/// <c>true</c> when count is zero (or non-zero if the parameter is <c>invert</c>). Bind it to
/// <c>IsVisible</c>: Avalonia has no <c>Visibility</c> enum, so this replaces WPF's CountToVisibility.
/// </summary>
public class CountToBoolConverter : MarkupExtension, IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var invert = parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase);
        var count = value switch
        {
            int i => i,
            System.Collections.ICollection c => c.Count,
            _ => 0
        };
        var isZero = count == 0;
        return isZero != invert;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}
