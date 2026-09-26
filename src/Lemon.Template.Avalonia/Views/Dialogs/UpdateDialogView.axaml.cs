using Avalonia.Controls;
using Lemon.Template.Avalonia.Commons;
using Lemon.Template.Avalonia.Infrastructures.Attributes;

namespace Lemon.Template.Avalonia.Views.Dialogs
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
