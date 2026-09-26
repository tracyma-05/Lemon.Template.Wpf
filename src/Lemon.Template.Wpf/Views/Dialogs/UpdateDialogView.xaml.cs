using Lemon.Template.Wpf.Commons;
using Lemon.Template.Wpf.Infrastructures.Attributes;
using System.Windows.Controls;

namespace Lemon.Template.Wpf.Views.Dialogs
{
    [KeyedService(Constants.UpdateDialog, typeof(UserControl))]
    public partial class UpdateDialogView : UserControl
    {
        public UpdateDialogView()
        {
            InitializeComponent();
        }
    }
}
