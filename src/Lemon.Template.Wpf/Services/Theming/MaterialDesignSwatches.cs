using System.Windows.Media;
using System.Linq;
using MaterialDesignColors;

namespace Lemon.Template.Wpf.Services.Theming;

/// <summary>Default swatches aligned with <c>App.xaml</c> BundledTheme (DeepPurple / Lime).</summary>
public static class MaterialDesignSwatches
{
    public static Color DefaultPrimary { get; } = SwatchHelper.Lookup[(MaterialDesignColor)PrimaryColor.DeepPurple];

    public static Color DefaultSecondary { get; } = SwatchHelper.Lookup[(MaterialDesignColor)SecondaryColor.Lime];

    public static IReadOnlyList<string> PrimarySwatchNames { get; } = Enum.GetNames<PrimaryColor>();

    public static IReadOnlyList<string> SecondarySwatchNames { get; } = Enum.GetNames<SecondaryColor>();

    public static Color ColorFromPrimaryName(string name) =>
        SwatchHelper.Lookup[(MaterialDesignColor)Enum.Parse<PrimaryColor>(name, true)];

    public static Color ColorFromSecondaryName(string name) =>
        SwatchHelper.Lookup[(MaterialDesignColor)Enum.Parse<SecondaryColor>(name, true)];

    public static string NameForPrimary(Color color) =>
        PrimarySwatchNames.FirstOrDefault(n => ColorsClose(ColorFromPrimaryName(n), color)) ?? PrimaryColor.DeepPurple.ToString();

    public static string NameForSecondary(Color color) =>
        SecondarySwatchNames.FirstOrDefault(n => ColorsClose(ColorFromSecondaryName(n), color)) ?? SecondaryColor.Lime.ToString();

    private static bool ColorsClose(Color a, Color b) =>
        Math.Abs(a.R - b.R) < 8 && Math.Abs(a.G - b.G) < 8 && Math.Abs(a.B - b.B) < 8;
}
