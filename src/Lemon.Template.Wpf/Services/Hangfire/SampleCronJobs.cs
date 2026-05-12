using Serilog;

namespace Lemon.Template.Wpf.Services.Hangfire;

/// <summary>
/// Static entry points for Hangfire recurring jobs (Hangfire requires serializable method bodies).
/// </summary>
public static class SampleCronJobs
{
    public static void WriteHeartbeat()
    {
        Log.Debug("Hangfire sample recurring job: heartbeat");
    }
}
