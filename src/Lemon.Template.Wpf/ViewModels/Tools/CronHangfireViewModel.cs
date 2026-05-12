using CommunityToolkit.Mvvm.ComponentModel;
using Lemon.Template.Wpf.Services.Hangfire;

namespace Lemon.Template.Wpf.ViewModels.Tools;

public partial class CronHangfireViewModel : ObservableObject
{
    public CronHangfireViewModel(HangfireLocalDashboardHost host)
    {
        if (!string.IsNullOrEmpty(host.DashboardUrl))
            DashboardUri = new Uri(host.DashboardUrl);
    }

    [ObservableProperty]
    private Uri? _dashboardUri;
}
