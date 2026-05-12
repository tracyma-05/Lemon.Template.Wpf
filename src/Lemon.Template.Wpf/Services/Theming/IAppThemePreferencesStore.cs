namespace Lemon.Template.Wpf.Services.Theming;

public interface IAppThemePreferencesStore
{
    /// <summary>No row yet: keep <c>App.xaml</c> BundledTheme until the user saves.</summary>
    AppThemeSnapshot? Load();

    void Save(AppThemeSnapshot snapshot);
}
