using Lemon.Template.Wpf.Commons;
using Lemon.Template.Wpf.Infrastructures.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace Lemon.Template.Wpf.Views.Tools;

[NavigationRegister(Constants.Cron, Constants.MainRegion, typeof(UserControl), Constants.CronIcon, ServiceLifetime.Transient, DisplayOrder = 11)]
public partial class CronHangfireView : UserControl
{
    public CronHangfireView()
    {
        InitializeComponent();
    }
}
