namespace Lemon.Template.Avalonia.Services.Theming;

public interface IAppThemePreferencesStore
{
    /// <summary>No row yet: keep the <c>App.axaml</c> MaterialTheme defaults until the user saves.</summary>
    AppThemeSnapshot? Load();

    void Save(AppThemeSnapshot snapshot);
}
