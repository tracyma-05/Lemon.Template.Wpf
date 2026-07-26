using CommunityToolkit.Mvvm.ComponentModel;
using Lemon.Template.Wpf.Infrastructures.Localization;

namespace Lemon.Template.Wpf.Models
{
    /// <summary>
    /// A "quick start" tile on the home page that jumps to a registered page.
    /// </summary>
    /// <remarks>
    /// <see cref="Title"/> deliberately reuses the menu label of the target page, so a shortcut can never
    /// drift from the navigation entry it points at, and only its description needs its own resource key.
    /// </remarks>
    public sealed class HomeShortcut : ObservableObject
    {
        private readonly string _descriptionKey;

        public HomeShortcut(string registerGroup, string icon, string descriptionKey)
        {
            RegisterGroup = registerGroup;
            Icon = icon;
            _descriptionKey = descriptionKey;

            // The home page lives for the lifetime of the app, so this subscription is never detached.
            LocalizationService.Instance.PropertyChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(Description));
            };
        }

        /// <summary>Registration key of the target page: <c>Group/Name</c>, or <c>Name</c> for a top-level page.</summary>
        public string RegisterGroup { get; }

        /// <summary>Material Design icon name (<c>PackIconKind</c>).</summary>
        public string Icon { get; }

        public string Title => LocalizationService.Instance.GetMenuTitle(PageName);

        public string Description => LocalizationService.Instance.GetString(_descriptionKey);

        private string PageName
        {
            get
            {
                var separator = RegisterGroup.LastIndexOf('/');
                return separator < 0 ? RegisterGroup : RegisterGroup[(separator + 1)..];
            }
        }
    }
}
