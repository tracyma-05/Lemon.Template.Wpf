using Lemon.Template.Wpf.Models;
using System.Collections.ObjectModel;

namespace Lemon.Template.Wpf.Commons
{
    public static class Constants
    {
        #region dialogs

        public const string RootIdentifier = "Root";

        public const string HostMessageBox = "HostMessageBoxView";

        public const string MessageBox = "MessageBoxView";

        #endregion

        #region region

        public const string MainRegion = "MainRegion";

        #endregion

        #region menu

        /// <summary>
        /// Landing page. A single-segment key registers a top-level entry with no children, which is why
        /// the icon pair collapses to one icon.
        /// </summary>
        public const string Home = "Home";
        public const string HomeIcon = "Home";

        /// <summary>Local Serilog file log: top-level menu Logs, child Local-Logs.</summary>
        public const string AppLocalLog = "Logs/Local-Logs";
        public const string AppLocalLogIcon = "TextBoxSearchOutline/TextBoxSearchOutline";

#if (EnableHangfire)
        public const string Cron = "Tools/Cron";
        public const string CronIcon = "Tools/Schedule";
#endif

        public const string ThemeAppearance = "Settings/Theme";
        public const string ThemeAppearanceIcon = "Cog/Palette";

        public const string Language = "Settings/Language";
        public const string LanguageIcon = "Cog/Translate";

        #endregion

        public static ObservableCollection<NavigationItem> NavigationItems = new();
    }
}
