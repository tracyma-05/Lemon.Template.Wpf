using CommunityToolkit.Mvvm.ComponentModel;
using Lemon.Template.Wpf.Services.Hangfire;

namespace Lemon.Template.Wpf.ViewModels.Tools;

public partial class CronHangfireViewModel : ObservableObject
{
    private static readonly Uri BlankPage = new("about:blank");

    public CronHangfireViewModel(HangfireLocalDashboardHost host)
    {
        if (!string.IsNullOrEmpty(host.DashboardUrl))
            DashboardUri = new Uri(host.DashboardUrl);
    }

    /// <summary>仪表盘地址；宿主未能启动时为 null，此时界面上的地址栏留空。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DashboardSource))]
    private Uri? _dashboardUri;

    /// <summary>供 WebView2 绑定：该控件的 Source 不接受 null，缺少地址时回落到空白页。</summary>
    public Uri DashboardSource => DashboardUri ?? BlankPage;
}
