using System;
using System.Collections.Generic;
using System.IO;
using Lemon.Template.Wpf.Services.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Lemon.Template.Wpf.Tests;

/// <summary>
/// Round-trips the language preference against a real SQLite file in a temp directory.
/// </summary>
public sealed class AppLanguagePreferencesSqliteStoreTests : IDisposable
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"lemon-lang-{Guid.NewGuid():N}", "test.db");

    private AppLanguagePreferencesSqliteStore CreateStore()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:SqliteDatabasePath"] = _databasePath,
            })
            .Build();

        return new AppLanguagePreferencesSqliteStore(
            configuration,
            NullLogger<AppLanguagePreferencesSqliteStore>.Instance);
    }

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (directory is not null && Directory.Exists(directory))
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // Temp cleanup only; a locked file must not fail the test run.
            }
        }
    }

    [Fact]
    public void Load_ReturnsNull_BeforeAnythingIsSaved()
    {
        var store = CreateStore();

        Assert.Null(store.Load());
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsTheCultureName()
    {
        var store = CreateStore();

        store.Save("zh-CN");

        Assert.Equal("zh-CN", store.Load());
    }

    [Fact]
    public void Save_OverwritesTheSingleRowRatherThanAccumulating()
    {
        var store = CreateStore();

        store.Save("zh-CN");
        store.Save("en");

        Assert.Equal("en", store.Load());
    }

    [Fact]
    public void Load_SeesAValueWrittenByAnotherStoreInstance()
    {
        CreateStore().Save("zh-CN");

        // A fresh instance models the next application launch.
        Assert.Equal("zh-CN", CreateStore().Load());
    }
}
