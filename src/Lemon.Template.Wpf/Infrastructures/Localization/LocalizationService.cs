using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text;
using System.Windows.Data;

namespace Lemon.Template.Wpf.Infrastructures.Localization
{
    /// <summary>
    /// Resource-backed implementation over <c>Resources/AppStrings.resx</c> and its satellite cultures.
    /// </summary>
    /// <remarks>
    /// Exposed as a singleton via <see cref="Instance"/> because XAML markup needs an ambient binding
    /// source (see <see cref="LocalizeExtension"/>); prefer injecting <see cref="ILocalizationService"/>
    /// in view models.
    /// </remarks>
    public sealed class LocalizationService : ILocalizationService
    {
        private static readonly ResourceManager Resources =
            new("Lemon.Template.Wpf.Resources.AppStrings", typeof(LocalizationService).Assembly);

        private static readonly CultureInfo[] Cultures =
        [
            CultureInfo.GetCultureInfo("en"),
            CultureInfo.GetCultureInfo("zh-CN"),
        ];

        private CultureInfo _currentCulture = Cultures[0];

        public static LocalizationService Instance { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public IReadOnlyList<CultureInfo> SupportedCultures => Cultures;

        public CultureInfo CurrentCulture => _currentCulture;

        public string this[string key] => GetString(key);

        public string GetString(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            // Missing keys surface as [Key] rather than an empty label, so a gap is visible instead of blank.
            return Resources.GetString(key, _currentCulture) ?? $"[{key}]";
        }

        public string Format(string key, params object?[] args) =>
            string.Format(_currentCulture, GetString(key), args);

        public string GetMenuTitle(string routeName)
        {
            if (string.IsNullOrEmpty(routeName))
            {
                return string.Empty;
            }

            var translated = Resources.GetString($"Menu_{ToResourceKeySegment(routeName)}", _currentCulture);
            return string.IsNullOrEmpty(translated) ? routeName : translated;
        }

        public void SetCulture(CultureInfo culture)
        {
            ArgumentNullException.ThrowIfNull(culture);

            if (_currentCulture.Equals(culture))
            {
                return;
            }

            _currentCulture = culture;

            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;

            // Binding.IndexerName ("Item[]") tells WPF every indexer binding on this source is stale,
            // which is what makes the whole UI re-read its strings without a restart.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
        }

        /// <summary>
        /// Resolves a persisted culture name to a supported culture, falling back to the neutral culture.
        /// </summary>
        public CultureInfo ResolveSupportedCulture(string? cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName))
            {
                return MatchSystemCulture();
            }

            try
            {
                var requested = CultureInfo.GetCultureInfo(cultureName);
                return Cultures.FirstOrDefault(c => c.Equals(requested))
                       ?? Cultures.FirstOrDefault(c => c.TwoLetterISOLanguageName == requested.TwoLetterISOLanguageName)
                       ?? Cultures[0];
            }
            catch (CultureNotFoundException)
            {
                return Cultures[0];
            }
        }

        private static CultureInfo MatchSystemCulture()
        {
            var system = CultureInfo.CurrentUICulture;
            return Cultures.FirstOrDefault(c => c.Equals(system))
                   ?? Cultures.FirstOrDefault(c => c.TwoLetterISOLanguageName == system.TwoLetterISOLanguageName)
                   ?? Cultures[0];
        }

        /// <summary>Route names may contain characters that are awkward in resource keys (e.g. "Local-Logs").</summary>
        private static string ToResourceKeySegment(string routeName)
        {
            var builder = new StringBuilder(routeName.Length);
            foreach (var c in routeName)
            {
                builder.Append(char.IsLetterOrDigit(c) ? c : '_');
            }

            return builder.ToString();
        }
    }
}
