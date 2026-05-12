using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Wpf.Services.Hangfire;

/// <summary>
/// Hosts Hangfire Dashboard and <see cref="BackgroundJobServer"/> on loopback (embedded Kestrel) using shared SQLite storage.
/// </summary>
public class HangfireLocalDashboardHost : ISingletonDependency, IAsyncDisposable
{
    private readonly JobStorage _jobStorage;
    private readonly ILogger<HangfireLocalDashboardHost> _logger;
    private readonly IConfiguration _configuration;
    private WebApplication? _app;

    public HangfireLocalDashboardHost(
        JobStorage jobStorage,
        IConfiguration configuration,
        ILogger<HangfireLocalDashboardHost> logger)
    {
        _jobStorage = jobStorage;
        _configuration = configuration;
        _logger = logger;
    }

    public string DashboardUrl { get; private set; } = string.Empty;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_app is not null)
            return;

        var configured = _configuration["HangfireDashboard:Url"]?.Trim();
        var useUrls = string.IsNullOrEmpty(configured)
            ? "http://127.0.0.1:0"
            : configured;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, useUrls);
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton(_jobStorage);
        builder.Services.AddHangfire((_, configuration) => configuration.UseStorage(_jobStorage));
        builder.Services.AddHangfireServer();

        _app = builder.Build();
        _app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[] { new LocalHangfireDashboardAuthorizationFilter() }
        });
        _app.MapGet("/", () => Results.Redirect("/hangfire"));

        await _app.StartAsync(cancellationToken).ConfigureAwait(false);

        var addresses = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        var baseUrl = addresses?.Addresses.FirstOrDefault()?.TrimEnd('/') ?? "http://127.0.0.1";
        DashboardUrl = $"{baseUrl}/hangfire";
        _logger.LogInformation("Hangfire dashboard: {Url}", DashboardUrl);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is null)
            return;

        try
        {
            await _app.StopAsync().ConfigureAwait(false);
            await _app.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hangfire dashboard host shutdown failed.");
        }
        finally
        {
            _app = null;
        }
    }

    private sealed class LocalHangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var remote = context.GetHttpContext().Connection.RemoteIpAddress;
            if (remote is null)
                return false;

            return System.Net.IPAddress.IsLoopback(remote);
        }
    }
}
