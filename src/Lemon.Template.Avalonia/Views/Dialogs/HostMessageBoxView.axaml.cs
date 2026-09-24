using Lemon.Template.Avalonia.Infrastructures.Attributes;
using Avalonia.Controls;

namespace Lemon.Template.Avalonia.Views.Dialogs
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