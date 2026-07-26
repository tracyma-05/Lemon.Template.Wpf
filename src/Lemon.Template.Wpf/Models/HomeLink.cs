using CommunityToolkit.Mvvm.ComponentModel;
using Lemon.Template.Wpf.Infrastructures.Localization;

namespace Lemon.Template.Wpf.Models
{
    /// <summary>An external link shown in the home page's information-links row.</summary>
    public sealed class HomeLink : ObservableObject
    {
        private readonly string _titleKey;

        public HomeLink(string titleKey, string icon, string url)
        {
            _titleKey = titleKey;
            Icon = icon;
            Url = url;

            // The home page lives for the lifetime of the app, so this subscription is never detached.
            LocalizationService.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Title));
        }

        /// <summary>Material Design icon name (<c>PackIconKind</c>).</summary>
        public string Icon { get; }

        /// <summary>Absolute http(s) URL; other schemes are refused when the link is activated.</summary>
        public string Url { get; }

        public string Title => LocalizationService.Instance.GetString(_titleKey);
    }
}
