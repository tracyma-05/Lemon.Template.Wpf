namespace Lemon.Template.Wpf.Services.Localization;

/// <summary>Persists the chosen interface language across runs.</summary>
public interface IAppLanguagePreferencesStore
{
    /// <summary>The stored culture name (e.g. <c>zh-CN</c>), or null when the user never chose one.</summary>
    string? Load();

    void Save(string cultureName);
}
