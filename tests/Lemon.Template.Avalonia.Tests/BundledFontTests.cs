using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Xunit;

namespace Lemon.Template.Avalonia.Tests;

/// <summary>
/// The UI and code fonts are bundled in Assets/Fonts. These check that each requested weight really lands on
/// a bundled face: a missing resource silently falls back to a system font, and a variable font (which
/// Avalonia renders at its default instance) would turn every weight Thin.
/// </summary>
public class BundledFontTests
{
    [AvaloniaTheory]
    [InlineData(FontWeight.Normal, FontWeight.Normal)]
    [InlineData(FontWeight.Medium, FontWeight.Medium)]
    [InlineData(FontWeight.SemiBold, FontWeight.Bold)]
    [InlineData(FontWeight.Bold, FontWeight.Bold)]
    public void UiFont_ResolvesEachWeightToABundledNotoFace(FontWeight requested, FontWeight expected)
    {
        var face = Resolve("UiFont", "字重 Weight", requested);

        Assert.StartsWith("Noto Sans SC", face.FamilyName, StringComparison.Ordinal);
        Assert.Equal(expected, face.Weight);
    }

    [AvaloniaFact]
    public void CodeFont_ResolvesToBundledCascadiaMono()
    {
        var face = Resolve("CodeFont", "0O1lI --", FontWeight.Normal);

        Assert.Equal("Cascadia Mono", face.FamilyName);
        Assert.Equal(FontWeight.Normal, face.Weight);
    }

    private static (string FamilyName, FontWeight Weight) Resolve(string resourceKey, string text, FontWeight weight)
    {
        var family = (FontFamily)Application.Current!.FindResource(resourceKey)!;
        var block = new TextBlock { Text = text, FontFamily = family, FontWeight = weight };
        var window = new Window { Content = block };
        window.Show();

        try
        {
            var face = block.TextLayout.TextLines[0].TextRuns.OfType<ShapedTextRun>().First().GlyphRun.GlyphTypeface;
            return (face.FamilyName, face.Weight);
        }
        finally
        {
            window.Close();
        }
    }
}
