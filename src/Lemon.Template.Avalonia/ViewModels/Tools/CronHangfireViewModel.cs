using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Template.Avalonia.Infrastructures.Shell;
using Lemon.Template.Avalonia.Services.Hangfire;

namespace Lemon.Template.Avalonia.ViewModels.Tools;

public partial class CronHangfireViewModel : ObservableObject
{
    public CronHangfireViewModel(HangfireLocalDashboardHost host)
    {
        if (!string.IsNullOrEmpty(host.DashboardUrl))
            DashboardUri = new Uri(host.DashboardUrl);
    }

    /// <summary>仪表盘地址；宿主未能启动时为 null，此时界面上显示“不可用”提示。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDashboardAvailable))]
    [NotifyCanExecuteChangedFor(nameof(OpenDashboardCommand))]
    private Uri? _dashboardUri;

    public bool IsDashboardAvailable => DashboardUri is not null;

    /// <summary>Opens the loopback dashboard in the default browser (Windows and macOS alike).</summary>
    [RelayCommand(CanExecute = nameof(IsDashboardAvailable))]
    private void OpenDashboard() => BrowserLauncher.TryOpen(DashboardUri?.AbsoluteUri);
}
