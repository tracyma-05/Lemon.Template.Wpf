using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Lemon.Template.Wpf.Infrastructures.Localization;
using Lemon.Template.Wpf.Services.Localization;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Wpf.ViewModels.Settings;

public partial class LanguageViewModel : ObservableObject, ISingletonDependency
{
    private readonly ILocalizationService _localizationService;
    private readonly IAppLanguagePreferencesStore _languageStore;

    public LanguageViewModel(
        ILocalizationService localizationService,
        IAppLanguagePreferencesStore languageStore)
    {
        _localizationService = localizationService;
        _languageStore = languageStore;

        Languages = localizationService.SupportedCultures
            .Select(culture => new LanguageOption(culture))
            .ToList();

        _selectedLanguage = Languages.FirstOrDefault(
            option => option.Culture.Equals(localizationService.CurrentCulture)) ?? Languages[0];
    }

    public IReadOnlyList<LanguageOption> Languages { get; }

    [ObservableProperty]
    private LanguageOption _selectedLanguage;

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        if (value is null || _localizationService.CurrentCulture.Equals(value.Culture))
        {
            return;
        }

        _localizationService.SetCulture(value.Culture);
        _languageStore.Save(value.Culture.Name);
    }

    /// <summary>Display wrapper so the combo box shows the language in its own language.</summary>
    public sealed class LanguageOption(CultureInfo culture)
    {
        public CultureInfo Culture { get; } = culture;

        public string DisplayName { get; } = culture.NativeName;

        public override string ToString() => DisplayName;
    }
}
