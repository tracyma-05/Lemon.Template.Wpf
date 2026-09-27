using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Lemon.Template.Wpf.Services.Updates;

/// <summary>
/// Second half of an automatic update, run by the <em>new</em> version from its staging folder once the old
/// version has exited (<see cref="UpdateInstaller"/> is the first half). Checked at the very top of start-up,
/// before logging, the container or any window exist; it swaps the installation, starts it and exits.
/// </summary>
/// <remarks>
/// The swap never leaves a half-updated app: the installation is renamed to a backup first, the new files
/// are copied in, and files only the old installation had (logs, a local database, edited files the package
/// does not ship) are copied over from the backup, the same result as unzipping the package over the old
/// folder. Any failure removes the partial copy and renames the backup back. Either way the app is started
/// again, so the user is never left with nothing running.
/// </remarks>
public static class UpdateApplier
{
    public const string Argument = "--apply-update";

    /// <summary>Bumped only with a change to the arguments below; an old app starts the new updater.</summary>
    private const string ProtocolVersion = "1";

    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);
    private const int Attempts = 20; // about 10 s: antivirus and indexers hold files briefly after an exit

    public const string LogFileName = "update.log";

    /// <summary>The arguments <see cref="TryRun"/> understands, in order.</summary>
    internal static IReadOnlyList<string> BuildArguments(int processId, AppInstallation installation, string newRoot) =>
    [
        Argument,
        ProtocolVersion,
        processId.ToString(CultureInfo.InvariantCulture),
        installation.RootPath,
        newRoot,
        installation.ExecutableRelativePath,
        installation.WorkPath,
    ];

    /// <summary>
    /// When the process was started as the updater, performs the update and returns true (the caller exits
    /// with <paramref name="exitCode"/>); otherwise returns false straight away.
    /// </summary>
    public static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0 || args[0] != Argument)
        {
            return false;
        }

        if (args.Length != 7 || args[1] != ProtocolVersion ||
            !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var processId))
        {
            exitCode = 2;
            return true;
        }

        var installation = new AppInstallation(args[3], args[5]);
        exitCode = Run(processId, installation, newRoot: args[4], workPath: args[6]);
        return true;
    }

    private static int Run(int processId, AppInstallation installation, string newRoot, string workPath)
    {
        var log = new UpdateLog(Path.Combine(workPath, LogFileName));
        try
        {
            log.Write($"Updating {installation.RootPath} from {newRoot}.");
            if (!WaitForExit(processId))
            {
                // Nothing was touched; the old version is still running and keeps working.
                log.Write($"FAILED: process {processId} did not exit within {ExitTimeout.TotalSeconds:0} s.");
                return 1;
            }

            var backup = Path.Combine(workPath, "backup");
            try
            {
                Apply(installation.RootPath, newRoot, backup);
                log.Write("Installed.");
            }
            catch (Exception ex)
            {
                log.Write($"FAILED, the previous version was kept: {ex}");
                Start(installation);
                return 1;
            }

            Start(installation);
            TryDelete(backup);
            return 0;
        }
        catch (Exception ex)
        {
            log.Write($"FAILED: {ex}");
            return 1;
        }
    }

    /// <summary>
    /// Replaces <paramref name="installRoot"/> with <paramref name="newRoot"/>, keeping the files only the
    /// old installation had. Throws with the old installation restored when anything goes wrong.
    /// </summary>
    /// <param name="backupPath">Where the old installation is kept; left in place for the caller to delete.</param>
    /// <param name="copyFile">Test seam for the per-file copy (source, destination).</param>
    internal static void Apply(string installRoot, string newRoot, string backupPath, Action<string, string>? copyFile = null)
    {
        if (Directory.Exists(backupPath))
        {
            Directory.Delete(backupPath, recursive: true);
        }

        // Fails, with nothing changed, while anything still has a file of the installation open.
        Retry(() => Directory.Move(installRoot, backupPath));

        try
        {
            if (copyFile is null && OperatingSystem.IsMacOS())
            {
                // ditto keeps a bundle's symlinks, permissions, extended attributes and code signature.
                RunDitto(newRoot, installRoot);
            }
            else
            {
                CopyTree(newRoot, installRoot, copyFile ?? CopyFile, overwrite: true);
            }

            // Logs, a local database, files the package does not ship. Copied rather than moved, so the
            // backup stays complete until the update has succeeded.
            CopyTree(backupPath, installRoot, copyFile ?? CopyFile, overwrite: false);
        }
        catch
        {
            if (Directory.Exists(installRoot))
            {
                Retry(() => Directory.Delete(installRoot, recursive: true));
            }

            Retry(() => Directory.Move(backupPath, installRoot));
            throw;
        }
    }

    /// <summary>Copies <paramref name="source"/> into <paramref name="target"/>; keeps existing files unless <paramref name="overwrite"/>.</summary>
    private static void CopyTree(string source, string target, Action<string, string> copyFile, bool overwrite)
    {
        Directory.CreateDirectory(target);
        foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos())
        {
            var destination = Path.Combine(target, entry.Name);
            if (entry.LinkTarget is { } link)
            {
                // A bundle's framework symlinks stay symlinks rather than turning into copies.
                if (!Path.Exists(destination))
                {
                    if (entry is DirectoryInfo)
                    {
                        Directory.CreateSymbolicLink(destination, link);
                    }
                    else
                    {
                        File.CreateSymbolicLink(destination, link);
                    }
                }
            }
            else if (entry is DirectoryInfo)
            {
                CopyTree(entry.FullName, destination, copyFile, overwrite);
            }
            else if (overwrite || !File.Exists(destination))
            {
                copyFile(entry.FullName, destination);
            }
        }
    }

    private static void CopyFile(string source, string destination) => File.Copy(source, destination, overwrite: true);

    private static void RunDitto(string source, string target)
    {
        using var ditto = Process.Start(new ProcessStartInfo("/usr/bin/ditto") { ArgumentList = { source, target } })
                          ?? throw new IOException("Could not start ditto.");
        ditto.WaitForExit();
        if (ditto.ExitCode != 0)
        {
            throw new IOException($"ditto exited with code {ditto.ExitCode}.");
        }
    }

    private static bool WaitForExit(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.WaitForExit(ExitTimeout);
        }
        catch (ArgumentException)
        {
            return true; // already gone
        }
    }

    /// <summary>Starts the installed app: through LaunchServices for a bundle, so it gets a proper Dock entry.</summary>
    internal static void Start(AppInstallation installation)
    {
        var start = installation.IsMacBundle
            ? new ProcessStartInfo("/usr/bin/open") { ArgumentList = { "-n", installation.RootPath } }
            : new ProcessStartInfo(installation.ExecutablePath) { WorkingDirectory = installation.RootPath };
        start.UseShellExecute = false;
        Process.Start(start)?.Dispose();
    }

    private static void Retry(Action action)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < Attempts)
            {
                Thread.Sleep(RetryDelay);
            }
        }
    }

    private static void TryDelete(string path)
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
            // The new version removes the whole work folder on start-up anyway.
        }
    }

    /// <summary>Plain-text log next to the backup: Serilog is not configured in updater mode.</summary>
    private sealed class UpdateLog(string path)
    {
        public void Write(string message)
        {
            try
            {
                File.AppendAllText(path, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {message}{Environment.NewLine}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Logging must never be the reason an update fails.
            }
        }
    }
}
