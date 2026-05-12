using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Media;

namespace Lemon.Template.Wpf.Services.Theming;

public sealed class AppThemeService : IAppThemeService
{
    private readonly IAppThemePreferencesStore _themePreferencesStore;
    private readonly PaletteHelper _paletteHelper = new();

    public event EventHandler<bool>? DarkThemeChanged;

    public AppThemeService(IAppThemePreferencesStore themePreferencesStore)
    {
        _themePreferencesStore = themePreferencesStore;
    }

    public bool IsDarkTheme()
    {
        var theme = _paletteHelper.GetTheme();
        return theme.GetBaseTheme() == BaseTheme.Dark;
    }

    public Color GetPrimaryColor() => _paletteHelper.GetTheme().PrimaryMid.Color;

    public Color GetSecondaryColor() => _paletteHelper.GetTheme().SecondaryMid.Color;

    public void SetDarkTheme(bool isDark)
    {
        RunOnUi(() =>
        {
            var theme = _paletteHelper.GetTheme();
            theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
            _paletteHelper.SetTheme(theme);
        });

        PersistCurrentPalette(isDark);
        NotifyDarkThemeChanged(isDark);
    }

    public void ApplyAndPersistTheme(Color primary, Color secondary, bool isDark)
    {
        RunOnUi(() =>
        {
            var theme = _paletteHelper.GetTheme();
            theme.SetPrimaryColor(primary);
            theme.SetSecondaryColor(secondary);
            theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
            _paletteHelper.SetTheme(theme);
        });

        _themePreferencesStore.Save(new AppThemeSnapshot(
            isDark,
            ThemeColorArgb.Pack(primary),
            ThemeColorArgb.Pack(secondary)));

        NotifyDarkThemeChanged(isDark);
    }

    public void ResetDefaultSwatchesPersist()
    {
        var isDark = IsDarkTheme();
        var primary = MaterialDesignSwatches.DefaultPrimary;
        var secondary = MaterialDesignSwatches.DefaultSecondary;
        ApplyAndPersistTheme(primary, secondary, isDark);
    }

    public void ApplySnapshot(AppThemeSnapshot snapshot)
    {
        RunOnUi(() =>
        {
            var theme = _paletteHelper.GetTheme();
            theme.SetBaseTheme(snapshot.IsDark ? BaseTheme.Dark : BaseTheme.Light);
            if (snapshot.PrimaryArgb is { } p)
            {
                theme.SetPrimaryColor(ThemeColorArgb.Unpack(p));
            }

            if (snapshot.SecondaryArgb is { } s)
            {
                theme.SetSecondaryColor(ThemeColorArgb.Unpack(s));
            }

            _paletteHelper.SetTheme(theme);
        });

        NotifyDarkThemeChanged(snapshot.IsDark);
    }

    private void PersistCurrentPalette(bool isDark)
    {
        RunOnUi(() =>
        {
            var theme = _paletteHelper.GetTheme();
            _themePreferencesStore.Save(new AppThemeSnapshot(
                isDark,
                ThemeColorArgb.Pack(theme.PrimaryMid.Color),
                ThemeColorArgb.Pack(theme.SecondaryMid.Color)));
        });
    }

    private void NotifyDarkThemeChanged(bool isDark)
    {
        var dispatcher = Application.Current?.Dispatcher;
        void Raise() => DarkThemeChanged?.Invoke(this, isDark);

        if (dispatcher is null)
        {
            Raise();
            return;
        }

        if (dispatcher.CheckAccess())
        {
            Raise();
        }
        else
        {
            dispatcher.BeginInvoke(Raise);
        }
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action);
        }
    }
}
