using System;
using System.Globalization;
using System.Linq;
using Lemon.Template.Wpf.Infrastructures.Localization;
using Xunit;

namespace Lemon.Template.Wpf.Tests;

/// <summary>
/// Guards the resource plumbing: a missing satellite assembly or a renamed key degrades silently into
/// English-only (or bracketed) labels, which is easy to ship without noticing.
/// </summary>
public class LocalizationServiceTests : IDisposable
{
    private readonly CultureInfo? _originalUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
    private readonly CultureInfo? _originalCulture = CultureInfo.DefaultThreadCurrentCulture;

    public void Dispose()
    {
        CultureInfo.DefaultThreadCurrentUICulture = _originalUiCulture;
        CultureInfo.DefaultThreadCurrentCulture = _originalCulture;
    }

    [Fact]
    public void SupportedCultures_StartWithNeutralFallback()
    {
        var service = new LocalizationService();

        Assert.Equal("en", service.SupportedCultures[0].Name);
        Assert.Contains(service.SupportedCultures, c => c.Name == "zh-CN");
    }

    [Fact]
    public void GetString_ResolvesNeutralCulture()
    {
        var service = new LocalizationService();

        Assert.Equal("Theme and colours", service.GetString("Theme_Title"));
    }

    [Fact]
    public void GetString_ResolvesSatelliteCulture()
    {
        var service = new LocalizationService();

        service.SetCulture(CultureInfo.GetCultureInfo("zh-CN"));

        // Fails if the zh-CN satellite assembly did not get built or deployed.
        Assert.Equal("主题与颜色", service.GetString("Theme_Title"));
    }

    [Fact]
    public void GetString_MarksMissingKeysInsteadOfReturningBlank()
    {
        var service = new LocalizationService();

        Assert.Equal("[NoSuchKey]", service.GetString("NoSuchKey"));
    }

    [Fact]
    public void Format_SubstitutesPlaceholders()
    {
        var service = new LocalizationService();

        var text = service.Format("LocalLog_Loaded", @"C:\logs\log.txt", 42);

        Assert.Equal(@"C:\logs\log.txt — 42 KB.", text);
    }

    [Theory]
    [InlineData("Settings", "Settings")]
    [InlineData("Theme", "Theme")]
    [InlineData("Local-Logs", "Local logs")]
    public void GetMenuTitle_MapsRouteNamesThroughTheMenuKeyConvention(string routeName, string expected)
    {
        var service = new LocalizationService();

        Assert.Equal(expected, service.GetMenuTitle(routeName));
    }

    [Fact]
    public void GetMenuTitle_FallsBackToTheRouteNameWhenUntranslated()
    {
        var service = new LocalizationService();

        Assert.Equal("Reports", service.GetMenuTitle("Reports"));
    }

    [Fact]
    public void SetCulture_RaisesTheIndexerChangeThatRefreshesBindings()
    {
        var service = new LocalizationService();
        var changed = new System.Collections.Generic.List<string?>();
        service.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        service.SetCulture(CultureInfo.GetCultureInfo("zh-CN"));

        // "Item[]" is what invalidates every {Binding [Key]} in the UI.
        Assert.Contains("Item[]", changed);
        Assert.Contains(nameof(ILocalizationService.CurrentCulture), changed);
    }

    [Fact]
    public void SetCulture_IsANoOpForTheSameCulture()
    {
        var service = new LocalizationService();
        var raised = 0;
        service.PropertyChanged += (_, _) => raised++;

        service.SetCulture(service.CurrentCulture);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void SetCulture_RejectsNull()
    {
        var service = new LocalizationService();

        Assert.Throws<ArgumentNullException>(() => service.SetCulture(null!));
    }

    [Theory]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("zh-TW", "zh-CN")]   // same language, different region -> nearest supported
    [InlineData("en-GB", "en")]
    [InlineData("de-DE", "en")]      // unsupported -> neutral fallback
    [InlineData("not-a-culture", "en")]
    public void ResolveSupportedCulture_MapsOntoAShippedCulture(string requested, string expected)
    {
        var service = new LocalizationService();

        Assert.Equal(expected, service.ResolveSupportedCulture(requested).Name);
    }

    [Fact]
    public void ResolveSupportedCulture_WithNoStoredValueReturnsAShippedCulture()
    {
        var service = new LocalizationService();

        var resolved = service.ResolveSupportedCulture(null);

        Assert.Contains(resolved, service.SupportedCultures.ToList());
    }
}
