using System.Windows.Data;

namespace Lemon.Template.Wpf.Infrastructures.Localization
{
    /// <summary>
    /// XAML shorthand for a live-updating localized string: <c>Text="{loc:Localize Theme_Title}"</c>.
    /// </summary>
    /// <remarks>
    /// Derives from <see cref="Binding"/> so it is accepted anywhere a binding is, and so switching
    /// language refreshes it without reloading the view.
    /// </remarks>
    public sealed class LocalizeExtension : Binding
    {
        public LocalizeExtension(string key)
            : base($"[{key}]")
        {
            Source = LocalizationService.Instance;
            Mode = BindingMode.OneWay;
        }
    }
}
