using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Lemon.Template.Avalonia.Infrastructures.Data;

/// <summary>
/// Single SQLite database for the app (Hangfire, feature tables, preferences). Default file name matches the entry assembly name.
/// </summary>
public static class AppSqlitePaths
{
    private static string? _applicationName;

    /// <summary>Assembly simple name, e.g. <c>Lemon.Template.Avalonia</c>.</summary>
    public static string ApplicationName =>
        _applicationName ??= typeof(AppSqlitePaths).Assembly.GetName().Name ?? "Lemon.Template.Avalonia";

    /// <summary><c>%LocalApplicationData%\{ApplicationName}\</c></summary>
    public static string ApplicationDataFolder =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ApplicationName);

    /// <summary>
    /// <c>{ApplicationDataFolder}/Logs</c>: Serilog writes here and the Local-Logs page reads from here.
    /// </summary>
    /// <remarks>
    /// Not under the app's base directory: inside a macOS <c>.app</c> bundle (and under Program Files on
    /// Windows) that folder is read-only for the user.
    /// </remarks>
    public static string LogsFolder => Path.Combine(ApplicationDataFolder, "Logs");

    /// <summary>
    /// <c>App:SqliteDatabasePath</c>: absolute path, or relative to the app base directory.
    /// When empty, uses <see cref="ApplicationDataFolder"/> and file <c>{ApplicationName}.db</c>.
    /// </summary>
    public static string ResolveDatabaseFile(IConfiguration configuration)
    {
        var configured = configuration["App:SqliteDatabasePath"]?.Trim();
        if (!string.IsNullOrEmpty(configured))
        {
            return Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configured);
        }

        return Path.Combine(ApplicationDataFolder, $"{ApplicationName}.db");
    }
}
