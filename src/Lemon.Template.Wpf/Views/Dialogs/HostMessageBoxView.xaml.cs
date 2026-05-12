using Lemon.Template.Wpf.Infrastructures.Attributes;
using System.Windows.Controls;

namespace Lemon.Template.Wpf.Views.Dialogs
{
    [KeyedService(nameof(HostMessageBoxView), typeof(UserControl))]
    public partial class HostMessageBoxView : UserControl
    {
        public HostMessageBoxView()
        {
            InitializeComponent();
        }
    }
}