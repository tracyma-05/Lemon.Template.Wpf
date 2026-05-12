using Hangfire;
using Microsoft.Extensions.Options;
using Volo.Abp.Hangfire;

namespace Lemon.Template.Wpf.Services.Hangfire;

public sealed class ConfigureAbpHangfireJobStorage : IConfigureOptions<AbpHangfireOptions>
{
    private readonly JobStorage _jobStorage;

    public ConfigureAbpHangfireJobStorage(JobStorage jobStorage)
    {
        _jobStorage = jobStorage;
    }

    public void Configure(AbpHangfireOptions options)
    {
        options.Storage = _jobStorage;
    }
}
