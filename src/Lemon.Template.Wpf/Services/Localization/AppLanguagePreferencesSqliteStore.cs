using System.IO;
using Lemon.Template.Wpf.Infrastructures.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Lemon.Template.Wpf.Services.Localization;

/// <summary>
/// Single-row table in the shared application database, mirroring <c>AppThemePreferencesSqliteStore</c>.
/// </summary>
public sealed class AppLanguagePreferencesSqliteStore : IAppLanguagePreferencesStore
{
    private const string CreateTableSql =
        """
        CREATE TABLE IF NOT EXISTS app_language (
            id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
            culture_name TEXT NOT NULL
        );
        """;

    private readonly string _databasePath;
    private readonly ILogger<AppLanguagePreferencesSqliteStore> _logger;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public AppLanguagePreferencesSqliteStore(
        IConfiguration configuration,
        ILogger<AppLanguagePreferencesSqliteStore> logger)
    {
        _logger = logger;
        _databasePath = AppSqlitePaths.ResolveDatabaseFile(configuration);
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public string? Load()
    {
        _mutex.Wait();
        try
        {
            EnsureSchema();
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT culture_name FROM app_language WHERE id = 1;";
            var value = cmd.ExecuteScalar() as string;
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load the language preference from SQLite.");
            return null;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public void Save(string cultureName)
    {
        _mutex.Wait();
        try
        {
            EnsureSchema();
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO app_language (id, culture_name)
                VALUES (1, $culture)
                ON CONFLICT(id) DO UPDATE SET culture_name = excluded.culture_name;
                """;
            cmd.Parameters.AddWithValue("$culture", cultureName);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save the language preference to SQLite.");
        }
        finally
        {
            _mutex.Release();
        }
    }

    private void EnsureSchema()
    {
        using var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = CreateTableSql;
        cmd.ExecuteNonQuery();
    }
}
