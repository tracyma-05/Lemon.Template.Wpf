using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace Lemon.Template.Wpf.Infrastructures.Localization
{
    /// <summary>
    /// Resolves UI strings for the active culture and notifies bindings when that culture changes.
    /// </summary>
    public interface ILocalizationService : INotifyPropertyChanged
    {
        /// <summary>Cultures the app ships strings for. The first entry is the neutral/fallback culture.</summary>
        IReadOnlyList<CultureInfo> SupportedCultures { get; }

        /// <summary>The culture currently used to resolve strings.</summary>
        CultureInfo CurrentCulture { get; }

        /// <summary>Indexer so XAML can bind to a key: <c>{Binding [Some_Key], Source=...}</c>.</summary>
        string this[string key] { get; }

        string GetString(string key);

        /// <summary>Formats a resource whose value contains <c>{0}</c>-style placeholders.</summary>
        string Format(string key, params object?[] args);

        /// <summary>
        /// Resolves a menu label from a route name (e.g. <c>Local-Logs</c> -> <c>Menu_Local_Logs</c>),
        /// falling back to the route name itself when no translation exists.
        /// </summary>
        string GetMenuTitle(string routeName);

        void SetCulture(CultureInfo culture);
    }
}
