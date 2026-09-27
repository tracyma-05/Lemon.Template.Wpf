using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using Lemon.Template.Wpf.Infrastructures.Localization;
using Lemon.Template.Wpf.Services.Updates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Lemon.Template.Wpf.Tests;

/// <summary>
/// Automatic updates: checksums from the release, finding the app in a package, and the swap that must never
/// leave a half-updated or data-less installation behind.
/// </summary>
public sealed class UpdateInstallerTests : IDisposable
{
    private const string Exe = "MyApp.exe";
    private static readonly Uri PackageUrl = new("https://updates.example.com/myapp/MyApp-2.0.0-win-x64.zip");
    private static readonly string Sha = new('a', 64);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "update-tests-" + Guid.NewGuid().ToString("N"));

    public UpdateInstallerTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    #region Checksums in the release

    [Fact]
    public void ParseManifest_reads_per_package_checksums()
    {
        var manifest = UpdateService.ParseManifest(
            $$"""
            {
              "version": "2.0.0",
              "downloads": {
                "win-x64": { "url": "MyApp-win-x64.zip", "sha256": "{{Sha.ToUpperInvariant()}}" },
                "osx-arm64": "MyApp-osx-arm64.zip"
              }
            }
            """,
            new Uri("https://updates.example.com/myapp/latest.json"))!;

        var win = manifest.Downloads["win-x64"];
        Assert.Equal(new Uri("https://updates.example.com/myapp/MyApp-win-x64.zip"), win);
        Assert.Equal(Sha, manifest.Checksums[win]);
        Assert.False(manifest.Checksums.ContainsKey(manifest.Downloads["osx-arm64"]));
    }

    [Fact]
    public void ParseManifest_applies_the_release_checksum_to_a_single_package_only()
    {
        var single = UpdateService.ParseManifest(
            $$"""{ "version": "2.0.0", "downloads": { "win-x64": "a.zip" }, "sha256": "{{Sha}}" }""",
            new Uri("https://updates.example.com/"))!;
        Assert.Equal(Sha, single.Checksums[single.Downloads["win-x64"]]);

        var several = UpdateService.ParseManifest(
            $$"""{ "version": "2.0.0", "downloads": { "win-x64": "a.zip", "osx-arm64": "b.zip" }, "downloadUrl": "a.zip", "sha256": "{{Sha}}" }""",
            new Uri("https://updates.example.com/"))!;
        Assert.Equal(Sha, several.Checksums[several.DownloadUrl!]);
        Assert.False(several.Checksums.ContainsKey(several.Downloads["osx-arm64"]));
    }

    [Fact]
    public void ParseGitHubRelease_reads_asset_digests()
    {
        var manifest = UpdateService.ParseGitHubRelease(
            $$"""
            {
              "tag_name": "v2.0.0",
              "assets": [
                { "name": "MyApp-2.0.0-win-x64.zip", "browser_download_url": "{{PackageUrl}}", "digest": "sha256:{{Sha}}" }
              ]
            }
            """)!;

        Assert.Equal(Sha, manifest.Checksums[PackageUrl]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("zz" + "0000000000000000000000000000000000000000000000000000000000000000")]
    public void NormalizeSha256_rejects_anything_but_64_hex_digits(string? value) =>
        Assert.Null(UpdateService.NormalizeSha256(value));

    #endregion

    #region Where the app is installed

    [Fact]
    public void Detect_uses_the_exe_folder()
    {
        var exe = Path.Combine(_root, "MyApp", Exe);

        var installation = AppInstallation.Detect(exe)!;

        Assert.Equal(Path.Combine(_root, "MyApp"), installation.RootPath);
        Assert.Equal(Exe, installation.ExecutableRelativePath);
        Assert.False(installation.IsMacBundle);
        Assert.Equal(Path.Combine(_root, ".MyApp.update"), installation.WorkPath);
    }

    [Fact]
    public void Detect_uses_the_whole_bundle_on_macOS()
    {
        var exe = Path.Combine(_root, "MyApp.app", "Contents", "MacOS", "MyApp");

        var installation = AppInstallation.Detect(exe)!;

        Assert.Equal(Path.Combine(_root, "MyApp.app"), installation.RootPath);
        Assert.Equal(Path.Combine("Contents", "MacOS", "MyApp"), installation.ExecutableRelativePath);
        Assert.True(installation.IsMacBundle);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("dotnet")]
    [InlineData("dotnet.exe")]
    public void Detect_refuses_the_dotnet_host(string? process) =>
        Assert.Null(AppInstallation.Detect(process is null ? null : Path.Combine(_root, process)));

    [Fact]
    public void FindIn_finds_the_app_in_a_zipped_folder()
    {
        var extracted = Path.Combine(_root, "extracted");
        WriteFile(Path.Combine(extracted, "MyApp-2.0.0", Exe));
        var installation = new AppInstallation(Path.Combine(_root, "MyApp"), Exe);

        Assert.Equal(Path.Combine(extracted, "MyApp-2.0.0"), installation.FindIn(extracted));
        Assert.Null(new AppInstallation(Path.Combine(_root, "MyApp"), "Other.exe").FindIn(extracted));
    }

    #endregion

    #region The swap

    [Fact]
    public void Apply_installs_the_new_files_and_keeps_what_only_the_old_installation_had()
    {
        var install = Path.Combine(_root, "MyApp");
        WriteFile(Path.Combine(install, Exe), "old exe");
        WriteFile(Path.Combine(install, "appsettings.json"), "old settings");
        WriteFile(Path.Combine(install, "Logs", "log-20260101.txt"), "log");
        WriteFile(Path.Combine(install, "data", "app.db"), "database");

        var staged = Path.Combine(_root, "staging", "MyApp");
        WriteFile(Path.Combine(staged, Exe), "new exe");
        WriteFile(Path.Combine(staged, "appsettings.json"), "new settings");
        WriteFile(Path.Combine(staged, "New.dll"), "new dll");

        UpdateApplier.Apply(install, staged, Path.Combine(_root, "backup"), CopyFile);

        Assert.Equal("new exe", File.ReadAllText(Path.Combine(install, Exe)));
        Assert.Equal("new settings", File.ReadAllText(Path.Combine(install, "appsettings.json")));
        Assert.True(File.Exists(Path.Combine(install, "New.dll")));
        Assert.Equal("log", File.ReadAllText(Path.Combine(install, "Logs", "log-20260101.txt")));
        Assert.Equal("database", File.ReadAllText(Path.Combine(install, "data", "app.db")));
    }

    [Fact]
    public void Apply_restores_the_old_installation_when_copying_fails()
    {
        var install = Path.Combine(_root, "MyApp");
        WriteFile(Path.Combine(install, Exe), "old exe");
        WriteFile(Path.Combine(install, "Logs", "log.txt"), "log");

        var staged = Path.Combine(_root, "staging", "MyApp");
        WriteFile(Path.Combine(staged, Exe), "new exe");
        WriteFile(Path.Combine(staged, "Broken.dll"), "x");

        var error = Assert.Throws<IOException>(() => UpdateApplier.Apply(install, staged, Path.Combine(_root, "backup"),
            (source, destination) =>
            {
                if (source.EndsWith("Broken.dll", StringComparison.Ordinal))
                {
                    throw new IOException("disk full");
                }

                CopyFile(source, destination);
            }));

        Assert.Equal("disk full", error.Message);
        Assert.Equal("old exe", File.ReadAllText(Path.Combine(install, Exe)));
        Assert.Equal("log", File.ReadAllText(Path.Combine(install, "Logs", "log.txt")));
        Assert.False(File.Exists(Path.Combine(install, "Broken.dll")));
        Assert.False(Directory.Exists(Path.Combine(_root, "backup")));
    }

    [Fact]
    public void TryRun_ignores_normal_start_up_and_rejects_unknown_protocols()
    {
        Assert.False(UpdateApplier.TryRun([], out _));
        Assert.False(UpdateApplier.TryRun(["--some-flag"], out _));

        Assert.True(UpdateApplier.TryRun([UpdateApplier.Argument, "99", "1", "a", "b", "c", "d"], out var exitCode));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public void BuildArguments_round_trip_through_TryRun_validation()
    {
        var installation = new AppInstallation(Path.Combine(_root, "My App"), Exe);

        var arguments = UpdateApplier.BuildArguments(1234, installation, Path.Combine(_root, "staging", "My App"));

        Assert.Equal(UpdateApplier.Argument, arguments[0]);
        Assert.Equal(7, arguments.Count);
        Assert.Equal(installation.WorkPath, arguments[^1]);
    }

    #endregion

    #region Download, verify, unpack

    [Theory]
    [InlineData("https://updates.example.com/MyApp-2.0.0-win-x64.msi", true, false)]
    [InlineData("https://updates.example.com/MyApp-2.0.0-osx-arm64.dmg", true, false)]
    [InlineData("https://updates.example.com/MyApp-2.0.0-win-x64.zip", false, false)]
    [InlineData("https://updates.example.com/MyApp-2.0.0-win-x64.zip", true, true)]
    [InlineData("https://updates.example.com/releases/2.0.0/download/win-x64", true, true)]
    public void CanInstall_needs_a_zip_and_AutoInstall(string url, bool autoInstall, bool expected)
    {
        var installer = CreateInstaller(_ => throw new InvalidOperationException(), autoInstall);

        Assert.Equal(expected, installer.CanInstall(Available(new Uri(url))));
    }

    [Fact]
    public void CanInstall_is_false_for_the_release_page_and_without_an_installation()
    {
        var page = new Uri("https://updates.example.com/myapp");
        var manifest = new UpdateManifest(new Version(2, 0, 0, 0), null, null, null) { PageUrl = page };
        var toPage = new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, new Version(1, 0, 0, 0), manifest, null, page);

        Assert.False(CreateInstaller(_ => throw new InvalidOperationException()).CanInstall(toPage));
        Assert.False(CreateInstaller(_ => throw new InvalidOperationException(), withoutInstallation: true).CanInstall(Available(PackageUrl)));
    }

    [Fact]
    public async Task PrepareAsync_downloads_verifies_and_unpacks_the_package()
    {
        var zip = ZipOf(("MyApp-2.0.0/" + Exe, "new exe"), ("MyApp-2.0.0/New.dll", "dll"));
        var progress = new List<UpdateDownloadProgress>();
        var installer = CreateInstaller(_ => Bytes(zip));

        var prepared = await installer.PrepareAsync(Available(PackageUrl, Hash(zip)), new SyncProgress(progress.Add), CancellationToken.None);

        Assert.Equal("new exe", File.ReadAllText(Path.Combine(prepared.NewRoot, Exe)));
        Assert.StartsWith(Installation.WorkPath, prepared.NewRoot, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(Installation.WorkPath, "package.zip")));
        Assert.Equal(zip.Length, progress[^1].Received);
        Assert.Equal(1d, progress[^1].Fraction);
    }

    [Fact]
    public async Task PrepareAsync_rejects_a_package_whose_checksum_does_not_match()
    {
        var installer = CreateInstaller(_ => Bytes(ZipOf((Exe, "new exe"))));

        var error = await Assert.ThrowsAsync<UpdateInstallException>(() =>
            installer.PrepareAsync(Available(PackageUrl, Sha), null, CancellationToken.None));

        Assert.Equal("Update_Error_Checksum", error.Message);
        Assert.False(Directory.Exists(Installation.WorkPath));
    }

    [Fact]
    public async Task PrepareAsync_rejects_a_download_that_is_not_a_zip()
    {
        var installer = CreateInstaller(_ => Bytes("<html>sign in</html>"u8.ToArray()));

        var error = await Assert.ThrowsAsync<UpdateInstallException>(() =>
            installer.PrepareAsync(Available(PackageUrl), null, CancellationToken.None));

        Assert.Equal("Update_Error_NotZip", error.Message);
    }

    [Fact]
    public async Task PrepareAsync_rejects_a_package_of_another_app()
    {
        var installer = CreateInstaller(_ => Bytes(ZipOf(("Other.exe", "x"))));

        var error = await Assert.ThrowsAsync<UpdateInstallException>(() =>
            installer.PrepareAsync(Available(PackageUrl), null, CancellationToken.None));

        Assert.Equal($"Update_Error_PackageLayout:{Exe}", error.Message);
        Assert.False(Directory.Exists(Installation.WorkPath));
    }

    [Fact]
    public async Task PrepareAsync_reports_http_errors()
    {
        var installer = CreateInstaller(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var error = await Assert.ThrowsAsync<UpdateInstallException>(() =>
            installer.PrepareAsync(Available(PackageUrl), null, CancellationToken.None));

        Assert.Equal("Update_Error_Http:404", error.Message);
    }

    [Fact]
    public async Task CleanUpAfterUpdateAsync_removes_the_work_folder()
    {
        WriteFile(Path.Combine(Installation.WorkPath, "backup", Exe));
        WriteFile(Path.Combine(Installation.WorkPath, UpdateApplier.LogFileName), "Installed.");

        await UpdateInstaller.CleanUpAfterUpdateAsync(NullLogger.Instance, Installation);

        Assert.False(Directory.Exists(Installation.WorkPath));
    }

    #endregion

    private AppInstallation Installation => new(Path.Combine(_root, "MyApp"), Exe);

    private UpdateInstaller CreateInstaller(
        Func<HttpRequestMessage, HttpResponseMessage> respond, bool autoInstall = true, bool withoutInstallation = false)
    {
        var installation = withoutInstallation ? null : Installation;
        if (installation is not null)
        {
            WriteFile(installation.ExecutablePath, "old exe");
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Update:AutoInstall"] = autoInstall.ToString() })
            .Build();

        return new UpdateInstaller(configuration, new KeyEchoLocalization(), logger: null,
            new HttpClient(new StubHandler(respond)), installation);
    }

    private static UpdateCheckResult Available(Uri package, string? sha256 = null)
    {
        var manifest = new UpdateManifest(new Version(2, 0, 0, 0), null, null, null)
        {
            Downloads = new Dictionary<string, Uri> { ["win-x64"] = package },
            Checksums = sha256 is null ? new Dictionary<Uri, string>() : new Dictionary<Uri, string> { [package] = sha256 },
        };
        return new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, new Version(1, 0, 0, 0), manifest, null, package);
    }

    private static byte[] ZipOf(params (string Path, string Content)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(path).Open());
                writer.Write(content);
            }
        }

        return buffer.ToArray();
    }

    private static string Hash(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

    private static HttpResponseMessage Bytes(byte[] data) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };

    private static void WriteFile(string path, string content = "")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void CopyFile(string source, string destination) => File.Copy(source, destination, overwrite: true);

    /// <summary>Progress&lt;T&gt; posts to the thread pool; this reports inline so the test sees every value.</summary>
    private sealed class SyncProgress(Action<UpdateDownloadProgress> report) : IProgress<UpdateDownloadProgress>
    {
        public void Report(UpdateDownloadProgress value) => report(value);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

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
