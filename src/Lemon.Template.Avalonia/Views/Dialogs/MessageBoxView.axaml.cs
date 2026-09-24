using Lemon.Template.Avalonia.Infrastructures.Attributes;
using Avalonia.Controls;

namespace Lemon.Template.Avalonia.Views.Dialogs
{
    [KeyedService(nameof(MessageBoxView), typeof(UserControl))]
    public partial class MessageBoxView : UserControl
    {
        public MessageBoxView()
        {
            InitializeComponent();
        }
    }
}