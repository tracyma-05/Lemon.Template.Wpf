using System;
using System.Collections.Generic;
using System.IO;
using Lemon.Template.Wpf.Infrastructures.Data;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Lemon.Template.Wpf.Tests;

public class AppSqlitePathsTests
{
    private static IConfiguration ConfigurationWith(string? sqliteDatabasePath) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:SqliteDatabasePath"] = sqliteDatabasePath,
            })
            .Build();

    [Fact]
    public void ResolveDatabaseFile_FallsBackToLocalAppData_WhenNotConfigured()
    {
        var resolved = AppSqlitePaths.ResolveDatabaseFile(ConfigurationWith(null));

        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppSqlitePaths.ApplicationName,
            $"{AppSqlitePaths.ApplicationName}.db");

        Assert.Equal(expected, resolved);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveDatabaseFile_TreatsBlankAsUnconfigured(string configured)
    {
        var resolved = AppSqlitePaths.ResolveDatabaseFile(ConfigurationWith(configured));

        Assert.StartsWith(AppSqlitePaths.ApplicationDataFolder, resolved, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveDatabaseFile_HonoursAbsolutePath()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "lemon-template-tests", "custom.db");

        var resolved = AppSqlitePaths.ResolveDatabaseFile(ConfigurationWith(absolute));

        Assert.Equal(absolute, resolved);
    }

    [Fact]
    public void ResolveDatabaseFile_ResolvesRelativePathAgainstBaseDirectory()
    {
        var resolved = AppSqlitePaths.ResolveDatabaseFile(ConfigurationWith(@"data\app.db"));

        Assert.Equal(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"data\app.db"), resolved);
    }

    [Fact]
    public void ApplicationDataFolder_IsUnderLocalAppData()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.StartsWith(localAppData, AppSqlitePaths.ApplicationDataFolder, StringComparison.Ordinal);
    }
}
