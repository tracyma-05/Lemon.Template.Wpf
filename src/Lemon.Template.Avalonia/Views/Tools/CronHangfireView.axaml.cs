using Lemon.Template.Avalonia.Commons;
using Lemon.Template.Avalonia.Infrastructures.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Controls;

namespace Lemon.Template.Avalonia.Views.Tools;

[NavigationRegister(Constants.Cron, Constants.MainRegion, typeof(UserControl), Constants.CronIcon, ServiceLifetime.Transient, DisplayOrder = 11)]
public partial class CronHangfireView : UserControl
{
    public CronHangfireView()
    {
        InitializeComponent();
    }
}
