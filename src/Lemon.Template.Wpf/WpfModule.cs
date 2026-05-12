using System.IO;
using Hangfire;
using Hangfire.Storage.SQLite;
using Lemon.Template.Wpf.Infrastructures;
using Lemon.Template.Wpf.Infrastructures.Attributes;
using Lemon.Template.Wpf.Infrastructures.Data;
using Lemon.Template.Wpf.Services.Hangfire;
using Lemon.Template.Wpf.Services.Theming;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Windows;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs.Hangfire;
using Volo.Abp.Hangfire;
using Volo.Abp.Modularity;

namespace Lemon.Template.Wpf;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpBackgroundJobsHangfireModule))]
public class WpfModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();
        context.Services.AddSingleton<IConfiguration>(configuration);

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

        context.Services.AddSingleton<IAppThemePreferencesStore, AppThemePreferencesSqliteStore>();
        context.Services.AddSingleton<IAppThemeService, AppThemeService>();

        context.Services.AddTransient<ViewModels.Tools.CronHangfireViewModel>();

        context.Services.AddKeyedServicesFromAssembly(typeof(WpfModule).Assembly);
        context.Services.AddNavigationServiceFromAssembly(typeof(WpfModule).Assembly);

        EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) =>
        {
            if (s is FrameworkElement view)
            {
                ViewModelLocator.AutoWireViewModel(view, App.ServiceProvider);
            }
        }));
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var storage = context.ServiceProvider.GetRequiredService<JobStorage>();
        JobStorage.Current = storage;

        RecurringJob.AddOrUpdate(
            "sample-heartbeat",
            () => SampleCronJobs.WriteHeartbeat(),
            Cron.Hourly);

        var dashboard = context.ServiceProvider.GetRequiredService<HangfireLocalDashboardHost>();
        dashboard.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        var dashboard = context.ServiceProvider.GetService<HangfireLocalDashboardHost>();
        dashboard?.DisposeAsync().GetAwaiter().GetResult();
    }
}
