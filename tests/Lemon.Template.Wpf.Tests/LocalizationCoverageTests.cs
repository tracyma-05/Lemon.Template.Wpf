using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Lemon.Template.Wpf.Infrastructures.Localization;
using Xunit;

namespace Lemon.Template.Wpf.Tests;

/// <summary>
/// Every page and menu is expected in both languages. A key added to one resx only, or used in code and added
/// to neither, shows up at run time as English in the Chinese UI or as a bracketed "[Key]" label.
/// </summary>
public partial class LocalizationCoverageTests
{
    private static readonly ResourceManager Resources =
        new("Lemon.Template.Wpf.Resources.AppStrings", typeof(LocalizationService).Assembly);

    [Fact]
    public void ChineseAndNeutralResources_HaveTheSameKeys()
    {
        var neutral = Keys(CultureInfo.InvariantCulture);
        var chinese = Keys(CultureInfo.GetCultureInfo("zh-CN"));

        Assert.Empty(neutral.Except(chinese).Order());
        Assert.Empty(chinese.Except(neutral).Order());
    }

    [Fact]
    public void KeysUsedInSource_ExistInResources()
    {
        var keys = Keys(CultureInfo.InvariantCulture);
        var sourceRoot = Path.Combine(RepositoryRoot(), "src", "Lemon.Template.Wpf");

        var missing = Directory.EnumerateFiles(sourceRoot, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".xaml", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(f => KeyReference().Matches(File.ReadAllText(f)).Select(m => (File: Path.GetFileName(f), Key: m.Groups["key"].Value)))
            .Where(x => !keys.Contains(x.Key))
            .Select(x => $"{x.File}: {x.Key}")
            .Distinct()
            .ToList();

        Assert.Empty(missing);
    }

    private static HashSet<string> Keys(CultureInfo culture)
    {
        // tryParents: false, so the zh-CN set is only what the satellite itself contains.
        var set = Resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false)
                  ?? throw new InvalidOperationException($"No resources for '{culture.Name}'.");
        return set.Cast<DictionaryEntry>().Select(e => (string)e.Key).ToHashSet(StringComparer.Ordinal);
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    // {loc:Localize Key}, <loc:LocalizeExtension Key="Key" />, GetString("Key") and Format("Key", …).
    [GeneratedRegex(@"loc:Localize (?<key>\w+)\}|LocalizeExtension Key=""(?<key>\w+)""|(?:GetString|Format)\(""(?<key>[A-Z]\w+_\w+)""")]
    private static partial Regex KeyReference();
}
