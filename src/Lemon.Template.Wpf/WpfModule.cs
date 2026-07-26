#if (EnableHangfire)
using System.IO;
using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.SQLite;
using Lemon.Template.Wpf.Infrastructures.Data;
using Lemon.Template.Wpf.Services.Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs.Hangfire;
using Volo.Abp.Hangfire;
#endif
using Lemon.Template.Wpf.Infrastructures.Attributes;
using Lemon.Template.Wpf.Infrastructures.Localization;
using Lemon.Template.Wpf.Services.Localization;
using Lemon.Template.Wpf.Services.Theming;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace Lemon.Template.Wpf;

[DependsOn(
    typeof(AbpAutofacModule)
#if (EnableHangfire)
    , typeof(AbpBackgroundJobsHangfireModule)
#endif
    )]
public class WpfModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();
        context.Services.AddSingleton<IConfiguration>(configuration);

#if (EnableHangfire)
        context.Services.AddSingleton<JobStorage>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var databasePath = AppSqlitePaths.ResolveDatabaseFile(configuration);
            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            return new SQLiteStorage(databasePath);
        });
        context.Services.AddSingleton<IConfigureOptions<AbpHangfireOptions>, ConfigureAbpHangfireJobStorage>();
        context.Services.AddSingleton<HangfireLocalDashboardHost>();
        context.Services.AddTransient<ViewModels.Tools.CronHangfireViewModel>();
#endif

        context.Services.AddSingleton<IAppThemePreferencesStore, AppThemePreferencesSqliteStore>();
        context.Services.AddSingleton<IAppThemeService, AppThemeService>();

        context.Services.AddSingleton<IAppLanguagePreferencesStore, AppLanguagePreferencesSqliteStore>();
        // Same instance XAML binds to through LocalizationService.Instance, so DI and markup stay in sync.
        context.Services.AddSingleton<ILocalizationService>(LocalizationService.Instance);

        context.Services.AddKeyedServicesFromAssembly(typeof(WpfModule).Assembly);
        context.Services.AddNavigationServiceFromAssembly(typeof(WpfModule).Assembly);
    }

#if (EnableHangfire)
    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        var serviceProvider = context.ServiceProvider;

        var storage = serviceProvider.GetRequiredService<JobStorage>();
        JobStorage.Current = storage;

        // Creates the Hangfire SQLite schema on first run — file I/O, so keep it off the UI thread
        // that is awaiting startup.
        await Task.Run(() => RecurringJob.AddOrUpdate(
            "sample-heartbeat",
            () => SampleCronJobs.WriteHeartbeat(),
            Cron.Hourly));

        var logger = serviceProvider.GetRequiredService<ILogger<WpfModule>>();
        await Task.Run(() => PruneUnloadableRecurringJobs(storage, logger));

        var dashboard = serviceProvider.GetRequiredService<HangfireLocalDashboardHost>();
        await dashboard.StartAsync();
    }

    /// <summary>
    /// Drops recurring jobs whose job type no longer exists in this assembly.
    /// </summary>
    /// <remarks>
    /// Recurring jobs live in the SQLite database, not in code, so renaming or deleting a job class leaves
    /// an orphan behind that Hangfire logs as an error on every start and then disables. Only entries that
    /// genuinely fail to deserialize are removed — jobs added at runtime through the dashboard are left
    /// alone.
    /// </remarks>
    private static void PruneUnloadableRecurringJobs(JobStorage storage, ILogger logger)
    {
        try
        {
            using var connection = storage.GetConnection();

            foreach (var recurringJob in connection.GetRecurringJobs())
            {
                if (recurringJob.LoadException is null)
                {
                    continue;
                }

                logger.LogWarning(
                    "Removing recurring job '{JobId}': its job type can no longer be loaded ({Reason}).",
                    recurringJob.Id,
                    recurringJob.LoadException.Message);

                RecurringJob.RemoveIfExists(recurringJob.Id);
            }
        }
        catch (Exception ex)
        {
            // Housekeeping only; never block startup over it.
            logger.LogWarning(ex, "Could not prune stale recurring jobs.");
        }
    }

    public override async Task OnApplicationShutdownAsync(ApplicationShutdownContext context)
    {
        var dashboard = context.ServiceProvider.GetService<HangfireLocalDashboardHost>();
        if (dashboard is not null)
        {
            await dashboard.DisposeAsync();
        }
    }
#endif
}
