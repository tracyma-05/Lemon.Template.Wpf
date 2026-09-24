using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System.Globalization;

namespace Lemon.Template.Avalonia.Converters;

/// <summary>Maps log <c>WordWrap</c> flag to <see cref="TextWrapping"/>.</summary>
public class BoolToTextWrappingConverter : MarkupExtension, IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? TextWrapping.Wrap : TextWrapping.NoWrap;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is TextWrapping tw && tw == TextWrapping.Wrap;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}
