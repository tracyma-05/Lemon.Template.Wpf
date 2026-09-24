using Avalonia.Media;

namespace Lemon.Template.Avalonia.Services.Theming;

public interface IAppThemeService
{
    /// <summary>Raised after light/dark base theme changes (any source). Argument is <c>true</c> for dark.</summary>
    event EventHandler<bool>? DarkThemeChanged;

    bool IsDarkTheme();

    /// <summary>Light/dark only; persists current primary/secondary from the active theme.</summary>
    void SetDarkTheme(bool isDark);

    /// <summary>Apply palette + base theme and persist to SQLite.</summary>
    void ApplyAndPersistTheme(Color primary, Color secondary, bool isDark);

    /// <summary>Restore default swatches from <c>App.axaml</c>; keeps current light/dark.</summary>
    void ResetDefaultSwatchesPersist();

    void ApplySnapshot(AppThemeSnapshot snapshot);

    Color GetPrimaryColor();

    Color GetSecondaryColor();
}
