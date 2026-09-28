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

        /// <summary>
        /// Element form, for places that take a binding element — a <see cref="MultiBinding"/> child:
        /// <c>&lt;loc:LocalizeExtension Key="LocalLog_Loaded" /&gt;</c>.
        /// </summary>
        public LocalizeExtension()
        {
            Source = LocalizationService.Instance;
            Mode = BindingMode.OneWay;
        }

        public string Key
        {
            get => Path?.Path is { Length: > 2 } path ? path[1..^1] : string.Empty;
            set => Path = new System.Windows.PropertyPath($"[{value}]");
        }
    }
}
