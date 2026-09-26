using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using Lemon.Template.Wpf.Infrastructures.Localization;
using Lemon.Template.Wpf.Services.Updates;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Lemon.Template.Wpf.Tests;

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
        Assert.Equal("https://updates.example.com/myapp/files/MyApp-1.3.0.zip", manifest.DownloadUrl.AbsoluteUri);
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

        var result = await service.CheckAsync();

        Assert.Equal(expected, result.Status);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task CheckAsync_reports_http_errors_without_throwing()
    {
        var service = CreateService(true, ManifestUrl, _ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await service.CheckAsync();

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.Equal("Update_Error_Http:404", result.Error);
    }

    [Fact]
    public async Task CheckAsync_reports_unreachable_servers_without_throwing()
    {
        var service = CreateService(true, ManifestUrl, _ => throw new HttpRequestException("connection refused"));

        var result = await service.CheckAsync();

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.Equal("Update_Error_Network", result.Error);
    }

    [Fact]
    public async Task CheckAsync_reports_malformed_manifests()
    {
        var service = CreateService(true, ManifestUrl, _ => Json("""{ "version": "soon" }"""));

        var result = await service.CheckAsync();

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.Equal("Update_Error_Format", result.Error);
    }

    [Fact]
    public async Task CheckAsync_without_configuration_does_not_touch_the_network()
    {
        var service = CreateService(false, "", _ => throw new InvalidOperationException("no request expected"));

        var result = await service.CheckAsync();

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.Equal("Update_Error_NotConfigured", result.Error);
    }

    private static UpdateService CreateService(bool enabled, string url, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Update:Enabled"] = enabled.ToString(),
                ["Update:Url"] = url,
            })
            .Build();

        return new UpdateService(
            configuration,
            new KeyEchoLocalization(),
            logger: null,
            new HttpClient(new StubHandler(respond)),
            new Version(1, 1, 0, 0));
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
