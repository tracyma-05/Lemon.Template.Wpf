using Lemon.Template.Wpf.Infrastructures.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Wpf.Services.Updates;

/// <summary>Bytes received so far, and the size when the server announced it.</summary>
public readonly record struct UpdateDownloadProgress(long Received, long? Total)
{
    public double? Fraction => Total > 0 ? Math.Clamp((double)Received / Total.Value, 0, 1) : null;
}

/// <summary>An update downloaded, verified and unpacked, ready for <see cref="IUpdateInstaller.Launch"/>.</summary>
/// <param name="NewRoot">The new version inside the staging folder (an app folder or a <c>.app</c> bundle).</param>
public sealed record PreparedUpdate(AppInstallation Installation, string NewRoot, Version Version);

/// <summary>What the updater reported for the last automatic update, read by the new version at start-up.</summary>
/// <param name="Failed">The updater gave up and kept the previous version.</param>
/// <param name="Log">The updater's log.</param>
public sealed record UpdateCleanupResult(bool Failed, string Log);

/// <summary>An update that could not be prepared; the message is meant for the user.</summary>
public sealed class UpdateInstallException(string message, Exception? inner = null) : Exception(message, inner);

public interface IUpdateInstaller
{
    /// <summary>
    /// Whether <paramref name="result"/> can be installed in place: <c>Update:AutoInstall</c> is on, the
    /// package is a zip (not an installer or a web page) and the app folder and its parent are writable.
    /// Otherwise the dialog falls back to opening the download in the browser.
    /// </summary>
    bool CanInstall(UpdateCheckResult result);

    /// <summary>Downloads the package, verifies its checksum when the release publishes one and unpacks it next to the app.</summary>
    /// <exception cref="UpdateInstallException">The download or the package is unusable.</exception>
    /// <exception cref="OperationCanceledException">Cancelled; nothing is left behind.</exception>
    Task<PreparedUpdate> PrepareAsync(UpdateCheckResult result, IProgress<UpdateDownloadProgress>? progress, CancellationToken cancellationToken);

    /// <summary>
    /// Starts the new version as the updater (<see cref="UpdateApplier"/>). The caller must then shut the
    /// app down promptly: the updater waits for this process to exit before it swaps the files.
    /// </summary>
    void Launch(PreparedUpdate update);
}

/// <summary>
/// First half of an automatic update: download, verify and unpack into the work folder next to the app,
/// then start the new version from there in updater mode and let the app exit.
/// </summary>
public sealed class UpdateInstaller : IUpdateInstaller, ISingletonDependency
{
    // No overall timeout for a download of unknown size; a connection that stops delivering is cut instead.
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);
    private static readonly Lazy<HttpClient> SharedClient = new(() => UpdateService.CreateClient(Timeout.InfiniteTimeSpan, acceptJson: false));

    private static readonly string[] InstallerExtensions = [".msi", ".exe", ".dmg", ".pkg", ".msix", ".appx", ".appinstaller"];

    private readonly IConfiguration _configuration;
    private readonly ILocalizationService _localization;
    private readonly ILogger<UpdateInstaller> _logger;
    private readonly HttpClient _httpClient;
    private readonly AppInstallation? _installation;

    public UpdateInstaller(IConfiguration configuration, ILocalizationService localization, ILogger<UpdateInstaller> logger)
        : this(configuration, localization, logger, SharedClient.Value, AppInstallation.Current)
    {
    }

    /// <summary>Test seam: a stubbed transport and an installation in a temporary folder.</summary>
    internal UpdateInstaller(
        IConfiguration configuration,
        ILocalizationService localization,
        ILogger<UpdateInstaller>? logger,
        HttpClient httpClient,
        AppInstallation? installation)
    {
        _configuration = configuration;
        _localization = localization;
        _logger = logger ?? NullLogger<UpdateInstaller>.Instance;
        _httpClient = httpClient;
        _installation = installation;
    }

    private bool AutoInstall =>
        _configuration.GetSection(UpdateOptions.SectionName).Get<UpdateOptions>()?.AutoInstall ?? true;

    public bool CanInstall(UpdateCheckResult result)
    {
        var reason = WhyNotInstallable(result);
        if (reason is not null && result.Status == UpdateCheckStatus.UpdateAvailable)
        {
            _logger.LogInformation("Update {Version} will be offered as a download: {Reason}.", result.Latest?.Version, reason);
        }

        return reason is null;
    }

    private string? WhyNotInstallable(UpdateCheckResult result)
    {
        if (result.Status != UpdateCheckStatus.UpdateAvailable || result.DownloadUrl is not { } url || result.Latest is null)
        {
            return "no update";
        }

        if (!AutoInstall)
        {
            return "Update:AutoInstall is off";
        }

        if (url == result.Latest.PageUrl)
        {
            return "the release has no package for this platform";
        }

        var extension = Path.GetExtension(url.AbsolutePath);
        if (InstallerExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return $"the package is an installer ({extension})";
        }

        if (_installation is null)
        {
            return "the app runs through the dotnet host";
        }

        return IsWritable(_installation) ? null : $"{_installation.RootPath} or its parent folder is not writable";
    }

    public async Task<PreparedUpdate> PrepareAsync(
        UpdateCheckResult result, IProgress<UpdateDownloadProgress>? progress, CancellationToken cancellationToken)
    {
        if (WhyNotInstallable(result) is { } reason)
        {
            throw new InvalidOperationException($"This update cannot be installed automatically: {reason}.");
        }

        var installation = _installation!;
        var work = installation.WorkPath;
        var package = Path.Combine(work, "package.zip");
        var staging = Path.Combine(work, "staging");

        try
        {
            ResetFolder(work);

            await DownloadAsync(result.DownloadUrl!, package, progress, cancellationToken).ConfigureAwait(false);

            if (result.Latest!.Checksums.TryGetValue(result.DownloadUrl!, out var expected))
            {
                var actual = await HashAsync(package, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Update package checksum mismatch: expected {Expected}, got {Actual}.", expected, actual);
                    throw new UpdateInstallException(_localization.GetString("Update_Error_Checksum"));
                }
            }

            if (!IsZip(package))
            {
                throw new UpdateInstallException(_localization.GetString("Update_Error_NotZip"));
            }

            await Task.Run(() => Extract(package, staging), cancellationToken).ConfigureAwait(false);
            File.Delete(package);

            var newRoot = installation.FindIn(staging)
                          ?? throw new UpdateInstallException(_localization.Format(
                              "Update_Error_PackageLayout", Path.GetFileName(installation.ExecutableRelativePath)));

            if (!OperatingSystem.IsWindows())
            {
                // Zip tools do not always keep the executable bit.
                var executable = Path.Combine(newRoot, installation.ExecutableRelativePath);
                File.SetUnixFileMode(executable, File.GetUnixFileMode(executable) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute);
            }

            return new PreparedUpdate(installation, newRoot, result.Latest.Version);
        }
        catch (Exception ex) when (ex is not UpdateInstallException)
        {
            TryDeleteFolder(work);
            if (ex is OperationCanceledException)
            {
                throw;
            }

            _logger.LogWarning(ex, "Preparing update {Version} failed.", result.Latest?.Version);
            throw new UpdateInstallException(
                ex is HttpRequestException or TimeoutException
                    ? _localization.GetString("Update_Error_Network")
                    : _localization.Format("Update_Error_Install", ex.Message),
                ex);
        }
        catch
        {
            TryDeleteFolder(work);
            throw;
        }
    }

    public void Launch(PreparedUpdate update)
    {
        var start = new ProcessStartInfo(Path.Combine(update.NewRoot, update.Installation.ExecutableRelativePath))
        {
            UseShellExecute = false,
            // Not the app folder: a process whose working directory is inside it blocks the rename.
            WorkingDirectory = update.Installation.WorkPath,
        };
        foreach (var argument in UpdateApplier.BuildArguments(Environment.ProcessId, update.Installation, update.NewRoot))
        {
            start.ArgumentList.Add(argument);
        }

        _logger.LogInformation("Starting the updater for {Version}; the app exits now.", update.Version);
        using var _ = Process.Start(start) ?? throw new UpdateInstallException(_localization.Format("Update_Error_Install", "the updater did not start"));
    }

    /// <summary>
    /// Called once the app is up: records what the updater did in the app log and removes the work folder
    /// (backup, staging, updater log). Retries for a while, as the updater may still be exiting.
    /// </summary>
    /// <returns>The updater's result right after an automatic update, so the shell can tell the user; null otherwise.</returns>
    public static async Task<UpdateCleanupResult?> CleanUpAfterUpdateAsync(ILogger logger, AppInstallation? installation = null)
    {
        installation ??= AppInstallation.Current;
        if (installation is null || !Directory.Exists(installation.WorkPath))
        {
            return null;
        }

        UpdateCleanupResult? result = null;

        var logFile = Path.Combine(installation.WorkPath, UpdateApplier.LogFileName);
        try
        {
            if (File.Exists(logFile))
            {
                var text = await File.ReadAllTextAsync(logFile).ConfigureAwait(false);
                result = new UpdateCleanupResult(text.Contains("FAILED", StringComparison.Ordinal), text);
                if (result.Failed)
                {
                    logger.LogWarning("The last update did not install:{NewLine}{UpdateLog}", Environment.NewLine, text);
                }
                else
                {
                    logger.LogInformation("Update log:{NewLine}{UpdateLog}", Environment.NewLine, text);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Fire-and-forget from start-up: a locked log must not fault an unobserved task.
            logger.LogWarning(ex, "Could not read the update log {Path}.", logFile);
        }

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                Directory.Delete(installation.WorkPath, recursive: true);
                return result;
            }
            catch (DirectoryNotFoundException)
            {
                return result;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
        }

        logger.LogWarning("Could not remove the update work folder {Path}; it is retried on the next start.", installation.WorkPath);
        return result;
    }

    private async Task DownloadAsync(Uri url, string path, IProgress<UpdateDownloadProgress>? progress, CancellationToken cancellationToken)
    {
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stall.CancelAfter(StallTimeout);
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, stall.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new UpdateInstallException(_localization.Format("Update_Error_Http", (int)response.StatusCode));
            }

            var total = response.Content.Headers.ContentLength;
            await using var source = await response.Content.ReadAsStreamAsync(stall.Token).ConfigureAwait(false);
            await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            var buffer = new byte[81920];
            long received = 0;
            progress?.Report(new UpdateDownloadProgress(0, total));
            int read;
            while ((read = await source.ReadAsync(buffer, stall.Token).ConfigureAwait(false)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), stall.Token).ConfigureAwait(false);
                received += read;
                progress?.Report(new UpdateDownloadProgress(received, total));
                stall.CancelAfter(StallTimeout);
            }

            if (total is { } expected && received != expected)
            {
                throw new UpdateInstallException(_localization.GetString("Update_Error_Network"));
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The stall timer, not the user.
            throw new TimeoutException($"No data from {url} for {StallTimeout.TotalSeconds:0} s.");
        }
    }

    private static async Task<string> HashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    private static bool IsZip(string path)
    {
        Span<byte> header = stackalloc byte[4];
        using var stream = File.OpenRead(path);
        return stream.ReadAtLeast(header, 4, throwOnEndOfStream: false) == 4 &&
               header[0] == (byte)'P' && header[1] == (byte)'K' && header[2] == 3 && header[3] == 4;
    }

    private static void Extract(string package, string staging)
    {
        if (OperatingSystem.IsMacOS())
        {
            // ditto restores what `ditto -c -k` stored: symlinks, permissions and the bundle's signature.
            using var ditto = Process.Start(new ProcessStartInfo("/usr/bin/ditto") { ArgumentList = { "-x", "-k", package, staging } })
                              ?? throw new IOException("Could not start ditto.");
            ditto.WaitForExit();
            if (ditto.ExitCode != 0)
            {
                throw new IOException($"ditto exited with code {ditto.ExitCode}.");
            }

            return;
        }

        // Rejects entries that would land outside the staging folder ("../" paths).
        ZipFile.ExtractToDirectory(package, staging);
    }

    private static bool IsWritable(AppInstallation installation)
    {
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(installation.RootPath));
        return parent is not null && CanWriteIn(installation.RootPath) && CanWriteIn(parent);
    }

    private static bool CanWriteIn(string folder)
    {
        var probe = Path.Combine(folder, $".write-test-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void ResetFolder(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
    }

    private void TryDeleteFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not remove {Path}.", path);
        }
    }
}
