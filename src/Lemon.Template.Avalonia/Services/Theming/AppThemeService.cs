using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Material.Styles.Themes;

namespace Lemon.Template.Avalonia.Services.Theming;

/// <summary>
/// Reads and changes the <see cref="CustomMaterialTheme"/> declared in <c>App.axaml</c>.
/// </summary>
/// <remarks>
/// <para>
/// Light / dark is <see cref="Application.RequestedThemeVariant"/> and nothing else: the theme's
/// <c>BaseTheme="Inherit"</c> follows it, and so do the non-Material parts of the UI (scroll bars, menus).
/// Touching <c>BaseTheme</c> or <c>CurrentTheme</c> directly instead makes Material rebuild the palette
/// asynchronously, which then lands on top of whatever was just set — the toggle and the colours drift apart.
/// </para>
/// <para>
/// Primary / secondary are the theme's own <see cref="CustomMaterialTheme.PrimaryColor"/> /
/// <see cref="CustomMaterialTheme.SecondaryColor"/>, which every rebuild uses, so they survive a light / dark
/// switch. All access happens on the UI thread.
/// </para>
/// </remarks>
public sealed class AppThemeService : IAppThemeService
{
    private readonly IAppThemePreferencesStore _themePreferencesStore;
    private CustomMaterialTheme? _subscribedTheme;

    public event EventHandler<bool>? DarkThemeChanged;

    public AppThemeService(IAppThemePreferencesStore themePreferencesStore)
    {
        _themePreferencesStore = themePreferencesStore;
    }

    public bool IsDarkTheme() =>
        RunOnUi(() => Application.Current?.ActualThemeVariant == ThemeVariant.Dark);

    public Color GetPrimaryColor() =>
        RunOnUi(() => LocateTheme()?.PrimaryColor ?? MaterialDesignSwatches.DefaultPrimary);

    public Color GetSecondaryColor() =>
        RunOnUi(() => LocateTheme()?.SecondaryColor ?? MaterialDesignSwatches.DefaultSecondary);

    public void SetDarkTheme(bool isDark)
    {
        RunOnUi(() => Apply(isDark, primary: null, secondary: null));

        PersistCurrentPalette(isDark);
        NotifyDarkThemeChanged(isDark);
    }

    public void ApplyAndPersistTheme(Color primary, Color secondary, bool isDark)
    {
        RunOnUi(() => Apply(isDark, primary, secondary));

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
        Color? primary = snapshot.PrimaryArgb is { } p ? ThemeColorArgb.Unpack(p) : null;
        Color? secondary = snapshot.SecondaryArgb is { } s ? ThemeColorArgb.Unpack(s) : null;

        RunOnUi(() => Apply(snapshot.IsDark, primary, secondary));

        NotifyDarkThemeChanged(snapshot.IsDark);
    }

    private void PersistCurrentPalette(bool isDark)
    {
        _themePreferencesStore.Save(new AppThemeSnapshot(
            isDark,
            ThemeColorArgb.Pack(GetPrimaryColor()),
            ThemeColorArgb.Pack(GetSecondaryColor())));
    }

    private void Apply(bool isDark, Color? primary, Color? secondary)
    {
        if (Application.Current is not { } application)
        {
            return;
        }

        var theme = LocateTheme();
        if (theme is not null)
        {
            SubscribeOnce(theme);

            if (primary is { } p)
            {
                theme.PrimaryColor = p;
            }

            if (secondary is { } s)
            {
                theme.SecondaryColor = s;
            }
        }

        application.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;

        if (theme is not null)
        {
            AdjustForDark(theme);
        }
    }

    /// <summary>
    /// Re-applies <see cref="AdjustForDark"/> after every palette rebuild. The rebuild runs on a worker thread
    /// and raises <c>ThemeChangedEnd</c> there, so the handler hops back to the UI thread before touching the
    /// theme ("The calling thread cannot access this object" otherwise).
    /// </summary>
    private void SubscribeOnce(CustomMaterialTheme theme)
    {
        if (ReferenceEquals(_subscribedTheme, theme))
        {
            return;
        }

        _subscribedTheme = theme;
        theme.ThemeChangedEnd += (_, _) => Dispatcher.UIThread.Post(() => AdjustForDark(theme));
    }

    /// <summary>
    /// On a dark base, uses the light primary shade as the mid one.
    /// </summary>
    /// <remarks>
    /// The WPF template's BundledTheme lightens the primary on dark backgrounds (ColorAdjustment);
    /// Material.Avalonia does not, and a mid primary such as DeepPurple 500 on #303030 leaves the selection
    /// highlight, floating labels and toggles too dark to read. Skipped once applied, because setting
    /// CurrentTheme raises ThemeChangedEnd again.
    /// </remarks>
    private static void AdjustForDark(MaterialThemeBase theme)
    {
        if (theme.ActualBaseTheme != Material.Styles.Themes.Base.BaseThemeMode.Dark)
        {
            return;
        }

        var current = theme.CurrentTheme;
        if (current.PrimaryMid.Color == current.PrimaryLight.Color)
        {
            return;
        }

        var adjusted = current.ToMutable();
        adjusted.PrimaryMid = adjusted.PrimaryLight;
        theme.CurrentTheme = adjusted;
    }

    private static CustomMaterialTheme? LocateTheme() =>
        Application.Current?.LocateMaterialTheme<CustomMaterialTheme>();

    private void NotifyDarkThemeChanged(bool isDark)
    {
        void Raise() => DarkThemeChanged?.Invoke(this, isDark);

        if (Dispatcher.UIThread.CheckAccess())
        {
            Raise();
        }
        else
        {
            Dispatcher.UIThread.Post(Raise);
        }
    }

    private static void RunOnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Invoke(action);
        }
    }

    private static T RunOnUi<T>(Func<T> func) =>
        Dispatcher.UIThread.CheckAccess() ? func() : Dispatcher.UIThread.Invoke(func);
}
