using System.IO;
using Lemon.Template.Wpf.Infrastructures.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Lemon.Template.Wpf.Services.Theming;

public sealed class AppThemePreferencesSqliteStore : IAppThemePreferencesStore
{
    private const string CreateTableSql =
        """
        CREATE TABLE IF NOT EXISTS app_theme (
            id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
            is_dark INTEGER NOT NULL DEFAULT 0,
            primary_argb INTEGER,
            secondary_argb INTEGER
        );
        """;

    private readonly string _databasePath;
    private readonly ILogger<AppThemePreferencesSqliteStore> _logger;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public AppThemePreferencesSqliteStore(IConfiguration configuration, ILogger<AppThemePreferencesSqliteStore> logger)
    {
        _logger = logger;
        _databasePath = AppSqlitePaths.ResolveDatabaseFile(configuration);
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public AppThemeSnapshot? Load()
    {
        _mutex.Wait();
        try
        {
            EnsureSchema();
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT is_dark, primary_argb, secondary_argb FROM app_theme WHERE id = 1;";
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            var isDark = reader.GetInt32(0) != 0;
            int? primary = reader.IsDBNull(1) ? null : reader.GetInt32(1);
            int? secondary = reader.IsDBNull(2) ? null : reader.GetInt32(2);
            return new AppThemeSnapshot(isDark, primary, secondary);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load app theme preferences from SQLite.");
            return null;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public void Save(AppThemeSnapshot snapshot)
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
                INSERT INTO app_theme (id, is_dark, primary_argb, secondary_argb)
                VALUES (1, $dark, $primary, $secondary)
                ON CONFLICT(id) DO UPDATE SET
                    is_dark = excluded.is_dark,
                    primary_argb = excluded.primary_argb,
                    secondary_argb = excluded.secondary_argb;
                """;
            cmd.Parameters.AddWithValue("$dark", snapshot.IsDark ? 1 : 0);
            cmd.Parameters.AddWithValue("$primary", snapshot.PrimaryArgb.HasValue ? snapshot.PrimaryArgb.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("$secondary", snapshot.SecondaryArgb.HasValue ? snapshot.SecondaryArgb.Value : DBNull.Value);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save app theme preferences to SQLite.");
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
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = CreateTableSql;
            cmd.ExecuteNonQuery();
        }

        AddColumnIfMissing(connection, "primary_argb", "INTEGER");
        AddColumnIfMissing(connection, "secondary_argb", "INTEGER");
    }

    private static void AddColumnIfMissing(SqliteConnection connection, string columnName, string columnType)
    {
        using var check = connection.CreateCommand();
        check.CommandText = "PRAGMA table_info(app_theme);";
        using var reader = check.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE app_theme ADD COLUMN {columnName} {columnType};";
        alter.ExecuteNonQuery();
    }
}
