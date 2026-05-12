using Lemon.Template.Wpf.Infrastructures.Attributes;
using System.Windows.Controls;

namespace Lemon.Template.Wpf.Views.Dialogs
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