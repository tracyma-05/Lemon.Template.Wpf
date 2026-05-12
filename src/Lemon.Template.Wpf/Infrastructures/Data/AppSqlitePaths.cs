using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Lemon.Template.Wpf.Infrastructures.Data;

/// <summary>
/// Single SQLite database for the app (Hangfire, feature tables, preferences). Default file name matches the entry assembly name.
/// </summary>
public static class AppSqlitePaths
{
    private static string? _applicationName;

    /// <summary>Assembly simple name, e.g. <c>Lemon.Template.Wpf</c>.</summary>
    public static string ApplicationName =>
        _applicationName ??= typeof(AppSqlitePaths).Assembly.GetName().Name ?? "Lemon.Template.Wpf";

    /// <summary><c>%LocalApplicationData%\{ApplicationName}\</c></summary>
    public static string ApplicationDataFolder =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ApplicationName);

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
