using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using Lemon.Template.Avalonia.Infrastructures.Localization;
using Lemon.Template.Avalonia.Services.Updates;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Lemon.Template.Avalonia.Tests;

public class UpdateServiceTests
{
    private const string ManifestUrl = "https://updates.example.com/myapp/latest.json";

    [Theory]
    [InlineData("1.2", "1.2.0.0")]
    [InlineData("1.2.3", "1.2.3.0")]
    [InlineData("v1.2.3", "1.2.3.0")]
    [InlineData("1.2.3.4", "1.2.3.4")]
    [InlineData("1.2.3-beta.1+abc", "1.2.3.0")]
    [InlineData(" 2.0.0 ", "2.0.0.0")]
    public void TryParseVersion_accepts_common_forms(string text, string expected)
    {
        Assert.Equal(Version.Parse(expected), UpdateService.TryParseVersion(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("1")]
    public void TryParseVersion_rejects_garbage(string? text)
    {
        Assert.Null(UpdateService.TryParseVersion(text));
    }

    [Fact]
    public void ParseManifest_reads_fields_case_insensitively_and_resolves_relative_links()
    {
        var manifest = UpdateService.ParseManifest(
            """{ "Version": "1.3.0", "DownloadUrl": "files/MyApp-1.3.0.zip", "ReleaseNotes": "Faster", "PublishedAt": "2026-09-26T08:00:00Z" }""",
            new Uri(ManifestUrl));

        Assert.NotNull(manifest);
        Assert.Equal(new Version(1, 3, 0, 0), manifest.Version);
        Assert.Equal("https://updates.example.com/myapp/files/MyApp-1.3.0.zip", manifest.DownloadUrl!.AbsoluteUri);
        Assert.Equal("Faster", manifest.ReleaseNotes);
        Assert.Equal(new DateTimeOffset(2026, 9, 26, 8, 0, 0, TimeSpan.Zero), manifest.PublishedAt);
    }

    [Theory]
    [InlineData("""{ "downloadUrl": "https://x.test/a.zip" }""")]
    [InlineData("""{ "version": "1.0.0" }""")]
    [InlineData("""{ "version": "1.0.0", "downloadUrl": "file:///C:/Windows/notepad.exe" }""")]
    [InlineData("""[ 1, 2 ]""")]
    [InlineData("""not json""")]
    public void ParseManifest_rejects_incomplete_or_unsafe_manifests(string json)
    {
        Assert.Null(UpdateService.ParseManifest(json, new Uri(ManifestUrl)));
    }

    [Theory]
    [InlineData(false, ManifestUrl, false)]
    [InlineData(true, "", false)]
    [InlineData(true, "not a url", false)]
    [InlineData(true, "ftp://updates.example.com/latest.json", false)]
    [InlineData(true, ManifestUrl, true)]
    public void IsEnabled_requires_both_the_switch_and_an_http_url(bool enabled, string url, bool expected)
    {
        var service = CreateService(enabled, url, _ => throw new InvalidOperationException("no request expected"));

        Assert.Equal(expected, service.IsEnabled);
    }

    [Theory]
    [InlineData("1.2.0", UpdateCheckStatus.UpdateAvailable)]
    [InlineData("1.1.1", UpdateCheckStatus.UpdateAvailable)]
    [InlineData("1.1", UpdateCheckStatus.UpToDate)]
    [InlineData("1.1.0", UpdateCheckStatus.UpToDate)]
    [InlineData("1.0.9", UpdateCheckStatus.UpToDate)]
    public async Task CheckAsync_compares_the_manifest_with_the_running_version(string published, UpdateCheckStatus expected)
    {
        var service = CreateService(true, ManifestUrl, _ => Json($$"""{ "version": "{{published}}", "downloadUrl": "https://x.test/a.zip" }"""));

        var result = await service.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Status);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task CheckAsync_reports_http_errors_without_throwing()
    {
        var service = CreateService(true, ManifestUrl, _ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await service.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.Equal("Update_Error_Http:404", result.Error);
    }

    [Fact]
    public async Task CheckAsync_reports_unreachable_servers_without_throwing()
    {
        var service = CreateService(true, ManifestUrl, _ => throw new HttpRequestException("connection refused"));

        var result = await service.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.Equal("Update_Error_Network", result.Error);
    }

    [Fact]
    public async Task CheckAsync_reports_malformed_manifests()
    {
        var service = CreateService(true, ManifestUrl, _ => Json("""{ "version": "soon" }"""));

        var result = await service.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.Equal("Update_Error_Format", result.Error);
    }

    [Fact]
    public async Task CheckAsync_without_configuration_does_not_touch_the_network()
    {
        var service = CreateService(false, "", _ => throw new InvalidOperationException("no request expected"));

        var result = await service.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.Equal("Update_Error_NotConfigured", result.Error);
    }

    #region platforms

    [Theory]
    [InlineData("win-x64", "win-x64,win,any")]
    [InlineData("win-arm64", "win-arm64,win-x64,win,any")]
    [InlineData("osx-arm64", "osx-arm64,osx-x64,osx,any")]
    [InlineData("osx-x64", "osx-x64,osx,any")]
    [InlineData("any", "any")]
    public void Platform_candidates_fall_back_from_exact_to_emulated_to_any(string platform, string expected)
    {
        Assert.Equal(expected.Split(','), UpdatePlatform.Candidates(platform));
    }

    [Theory]
    [InlineData("MyApp-1.2.0-win-x64.zip", "win-x64")]
    [InlineData("MyApp-1.2.0-osx-arm64.zip", "osx-arm64")]
    [InlineData("MyApp_1.2.0_macOS_x86_64.dmg", "osx-x64")]
    [InlineData("myapp-darwin-aarch64.tar.gz", "osx-arm64")]
    [InlineData("MyApp-Setup-win64.exe", "win-x64")]
    [InlineData("gh_2.101.0_windows_386.msi", "win-x86")]
    [InlineData("gh_2.101.0_macOS_amd64.zip", "osx-x64")]
    [InlineData("MyApp-1.2.0-macos-universal.zip", "osx")]
    [InlineData("MyApp-1.2.0-windows.msi", "win")]
    [InlineData("MyApp-1.2.0.zip", null)]
    [InlineData("Winamp-1.0.zip", null)]
    [InlineData("gh_2.101.0_linux_armv6.deb", null)]
    public void Platform_is_read_from_package_file_names(string fileName, string? expected)
    {
        Assert.Equal(expected, UpdatePlatform.FromFileName(fileName));
    }

    #endregion

    #region per-platform manifests

    private const string PlatformManifest = """
        {
          "version": "1.3.0",
          "downloads": {
            "win-x64": "files/MyApp-1.3.0-win-x64.zip",
            "OSX-X64": "https://cdn.example.com/MyApp-1.3.0-osx-x64.zip",
            "linux-x64": "javascript:alert(1)"
          },
          "downloadUrl": "files/MyApp-1.3.0-win-x64.zip",
          "pageUrl": "https://updates.example.com/myapp"
        }
        """;

    [Fact]
    public void ParseManifest_reads_per_platform_downloads_and_drops_unsafe_links()
    {
        var manifest = UpdateService.ParseManifest(PlatformManifest, new Uri(ManifestUrl));

        Assert.NotNull(manifest);
        Assert.Equal(["osx-x64", "win-x64"], manifest.Downloads.Keys.Order());
        Assert.Equal("https://updates.example.com/myapp/files/MyApp-1.3.0-win-x64.zip", manifest.Downloads["win-x64"].AbsoluteUri);
        Assert.Equal("https://updates.example.com/myapp", manifest.PageUrl!.AbsoluteUri);
    }

    [Theory]
    [InlineData("win-x64", "https://updates.example.com/myapp/files/MyApp-1.3.0-win-x64.zip")]
    [InlineData("win-arm64", "https://updates.example.com/myapp/files/MyApp-1.3.0-win-x64.zip")]
    [InlineData("osx-arm64", "https://cdn.example.com/MyApp-1.3.0-osx-x64.zip")]
    // No package for Linux: the release page, never the Windows downloadUrl.
    [InlineData("linux-x64", "https://updates.example.com/myapp")]
    public async Task CheckAsync_picks_the_package_for_this_platform(string platform, string expected)
    {
        var service = CreateService(true, ManifestUrl, _ => Json(PlatformManifest), platform: platform);

        var result = await service.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal(expected, result.DownloadUrl!.AbsoluteUri);
    }

    [Fact]
    public async Task CheckAsync_fails_when_no_package_suits_and_there_is_no_page()
    {
        var service = CreateService(
            true,
            ManifestUrl,
            _ => Json("""{ "version": "1.3.0", "downloads": { "osx-arm64": "https://x.test/mac.zip" } }"""),
            platform: "win-x64");

        var result = await service.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.Equal("Update_Error_NoPackage:1.3.0,win-x64", result.Error);
    }

    #endregion

    #region GitHub Releases

    private const string GitHubRelease = """
        {
          "tag_name": "v1.3.0",
          "name": "1.3.0",
          "body": "- Mac support",
          "html_url": "https://github.com/acme/myapp/releases/tag/v1.3.0",
          "published_at": "2026-09-26T08:00:00Z",
          "assets": [
            { "name": "MyApp-1.3.0-win-x64.zip", "browser_download_url": "https://github.com/acme/myapp/releases/download/v1.3.0/MyApp-1.3.0-win-x64.zip" },
            { "name": "MyApp-1.3.0-win-x64.msi", "browser_download_url": "https://github.com/acme/myapp/releases/download/v1.3.0/MyApp-1.3.0-win-x64.msi" },
            { "name": "MyApp-1.3.0-osx-arm64.zip", "browser_download_url": "https://github.com/acme/myapp/releases/download/v1.3.0/MyApp-1.3.0-osx-arm64.zip" },
            { "name": "checksums.txt", "browser_download_url": "https://github.com/acme/myapp/releases/download/v1.3.0/checksums.txt" }
          ]
        }
        """;

    [Theory]
    [InlineData("acme/myapp", true)]
    [InlineData("https://github.com/acme/my.app.git", true)]
    [InlineData("https://github.com/acme/myapp/", true)]
    [InlineData("acme", false)]
    [InlineData("acme/myapp/extra", false)]
    [InlineData("acme/..", false)]
    [InlineData("", false)]
    public void IsEnabled_for_GitHub_requires_an_owner_and_repository(string repository, bool expected)
    {
        var service = CreateService(
            new() { ["Update:Enabled"] = "true", ["Update:Provider"] = "GitHub", ["Update:GitHubRepository"] = repository },
            _ => throw new InvalidOperationException("no request expected"));

        Assert.Equal(expected, service.IsEnabled);
    }

    [Fact]
    public void Unknown_providers_leave_the_feature_off()
    {
        var service = CreateService(
            new() { ["Update:Enabled"] = "true", ["Update:Provider"] = "Ftp", ["Update:Url"] = ManifestUrl },
            _ => throw new InvalidOperationException("no request expected"));

        Assert.False(service.IsEnabled);
    }

    [Theory]
    [InlineData("win-x64", "MyApp-1.3.0-win-x64.msi")] // the installer beats the zip
    [InlineData("osx-arm64", "MyApp-1.3.0-osx-arm64.zip")]
    [InlineData("osx-x64", "https://github.com/acme/myapp/releases/tag/v1.3.0")] // no Intel build: the release page
    public async Task CheckAsync_reads_the_latest_GitHub_release(string platform, string expected)
    {
        HttpRequestMessage? sent = null;
        var service = CreateService(
            new() { ["Update:Enabled"] = "true", ["Update:Provider"] = "github", ["Update:GitHubRepository"] = "acme/myapp" },
            request =>
            {
                sent = request;
                return Json(GitHubRelease);
            },
            platform);

        var result = await service.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal("https://api.github.com/repos/acme/myapp/releases/latest", sent!.RequestUri!.AbsoluteUri);
        Assert.Contains(sent.Headers.Accept, x => x.MediaType == "application/vnd.github+json");
        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal(new Version(1, 3, 0, 0), result.Latest!.Version);
        Assert.Equal("- Mac support", result.Latest.ReleaseNotes);
        Assert.EndsWith(expected, result.DownloadUrl!.AbsoluteUri);
    }

    [Fact]
    public void A_single_unlabelled_asset_is_offered_to_every_platform()
    {
        var manifest = UpdateService.ParseGitHubRelease("""
            { "tag_name": "2.0.0", "assets": [ { "name": "MyApp.zip", "browser_download_url": "https://x.test/MyApp.zip" } ] }
            """);

        Assert.Equal("https://x.test/MyApp.zip", manifest!.Downloads[UpdatePlatform.Any].AbsoluteUri);
    }

    [Fact]
    public async Task CheckAsync_explains_a_repository_without_releases()
    {
        var service = CreateService(
            new() { ["Update:Enabled"] = "true", ["Update:Provider"] = "GitHub", ["Update:GitHubRepository"] = "acme/myapp" },
            _ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await service.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.Equal("Update_Error_NoRelease", result.Error);
    }

    #endregion

    private static UpdateService CreateService(
        bool enabled,
        string url,
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        string platform = "win-x64") =>
        CreateService(new() { ["Update:Enabled"] = enabled.ToString(), ["Update:Url"] = url }, respond, platform);

    private static UpdateService CreateService(
        Dictionary<string, string?> settings,
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        string platform = "win-x64")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new UpdateService(
            configuration,
            new KeyEchoLocalization(),
            logger: null,
            new HttpClient(new StubHandler(respond)),
            new Version(1, 1, 0, 0),
            platform);
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    /// <summary>Returns keys instead of translations, so assertions do not depend on the resx text.</summary>
    private sealed class KeyEchoLocalization : ILocalizationService
    {
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }

        public IReadOnlyList<CultureInfo> SupportedCultures { get; } = [CultureInfo.InvariantCulture];

        public CultureInfo CurrentCulture => CultureInfo.InvariantCulture;

        public string this[string key] => key;

        public string GetString(string key) => key;

        public string Format(string key, params object?[] args) => $"{key}:{string.Join(',', args)}";

        public string GetMenuTitle(string routeName) => routeName;

        public void SetCulture(CultureInfo culture)
        {
        }
    }
}
