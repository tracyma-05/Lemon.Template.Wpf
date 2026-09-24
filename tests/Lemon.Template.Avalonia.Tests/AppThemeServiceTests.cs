using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Material.Styles.Themes;
using Material.Styles.Themes.Base;
using Lemon.Template.Avalonia.Services.Theming;
using Xunit;

namespace Lemon.Template.Avalonia.Tests;

/// <summary>
/// Runs against the CustomMaterialTheme declared in App.axaml (loaded by <see cref="TestAppBuilder"/>), so these
/// cover the real Material.Avalonia calls rather than a stand-in.
/// </summary>
public class AppThemeServiceTests
{
    private static readonly Color CustomPrimary = Color.FromRgb(0x12, 0x80, 0x40);
    private static readonly Color CustomSecondary = Color.FromRgb(0xE0, 0x40, 0x10);

    [AvaloniaFact]
    public void ApplyAndPersistTheme_AppliesAndSavesThePalette()
    {
        var store = new InMemoryThemeStore();
        var service = new AppThemeService(store);

        service.ApplyAndPersistTheme(CustomPrimary, CustomSecondary, isDark: true);

        Assert.True(service.IsDarkTheme());
        Assert.Equal(CustomPrimary, service.GetPrimaryColor());
        Assert.Equal(CustomSecondary, service.GetSecondaryColor());
        Assert.Equal(new AppThemeSnapshot(true, ThemeColorArgb.Pack(CustomPrimary), ThemeColorArgb.Pack(CustomSecondary)), store.Saved);

        service.ResetDefaultSwatchesPersist();
    }

    /// <summary>
    /// Switching light/dark must not throw away a custom palette: Material re-derives colours when the base
    /// theme changes, so the service has to carry the current primary/secondary across.
    /// </summary>
    [AvaloniaFact]
    public void SetDarkTheme_KeepsTheCustomPalette()
    {
        var service = new AppThemeService(new InMemoryThemeStore());
        service.ApplyAndPersistTheme(CustomPrimary, CustomSecondary, isDark: false);

        service.SetDarkTheme(true);

        Assert.True(service.IsDarkTheme());
        Assert.Equal(CustomPrimary, service.GetPrimaryColor());
        Assert.Equal(CustomSecondary, service.GetSecondaryColor());

        service.SetDarkTheme(false);
        Assert.False(service.IsDarkTheme());
        Assert.Equal(CustomPrimary, service.GetPrimaryColor());

        service.ResetDefaultSwatchesPersist();
    }

    /// <summary>
    /// The shell's dark toggle used to "not work": Material rebuilt the palette asynchronously after the switch
    /// and the rebuild landed on top of it. Wait for that rebuild and check the result is still what was asked.
    /// </summary>
    [AvaloniaFact]
    public void SetDarkTheme_SurvivesTheAsynchronousPaletteRebuild()
    {
        var service = new AppThemeService(new InMemoryThemeStore());
        service.ApplyAndPersistTheme(CustomPrimary, CustomSecondary, isDark: false);

        service.SetDarkTheme(true);
        Settle();

        var theme = Application.Current!.LocateMaterialTheme<CustomMaterialTheme>();
        Assert.True(service.IsDarkTheme());
        Assert.Equal(BaseThemeMode.Dark, theme.ActualBaseTheme);
        Assert.Equal(CustomPrimary, service.GetPrimaryColor());
        // Dark base: the mid shade is lifted to the light one for contrast.
        Assert.Equal(theme.CurrentTheme.PrimaryLight.Color, theme.CurrentTheme.PrimaryMid.Color);

        service.SetDarkTheme(false);
        Settle();

        Assert.False(service.IsDarkTheme());
        Assert.Equal(BaseThemeMode.Light, theme.ActualBaseTheme);
        Assert.Equal(CustomPrimary, service.GetPrimaryColor());

        service.ResetDefaultSwatchesPersist();
        Settle();
    }

    /// <summary>App.axaml hard-codes the start-up colours; "Reset" uses MaterialDesignSwatches. Keep them equal.</summary>
    [AvaloniaFact]
    public void AppDefaults_MatchTheResetSwatches()
    {
        var theme = Application.Current!.LocateMaterialTheme<CustomMaterialTheme>();

        Assert.Equal(MaterialDesignSwatches.DefaultPrimary, theme.PrimaryColor);
        Assert.Equal(MaterialDesignSwatches.DefaultSecondary, theme.SecondaryColor);
    }

    /// <summary>Lets Material's background rebuild finish and its UI-thread follow-ups run.</summary>
    private static void Settle()
    {
        for (var i = 0; i < 50; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
    }

    [AvaloniaFact]
    public void DarkThemeChanged_ReportsTheNewBase()
    {
        var service = new AppThemeService(new InMemoryThemeStore());
        bool? reported = null;
        service.DarkThemeChanged += (_, isDark) => reported = isDark;

        service.SetDarkTheme(true);
        Assert.True(reported);

        service.SetDarkTheme(false);
        Assert.False(reported);
    }

    private sealed class InMemoryThemeStore : IAppThemePreferencesStore
    {
        public AppThemeSnapshot? Saved { get; private set; }

        public AppThemeSnapshot? Load() => Saved;

        public void Save(AppThemeSnapshot snapshot) => Saved = snapshot;
    }
}
