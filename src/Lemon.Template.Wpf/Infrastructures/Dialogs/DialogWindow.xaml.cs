using System.Windows;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Wpf.Infrastructures.Dialogs
{
    public partial class DialogWindow : Window, IDialogWindow, ISingletonDependency
    {
        public IDialogResult Result { get; set; }

        public DialogWindow()
        {
            InitializeComponent();
        }
    }
}