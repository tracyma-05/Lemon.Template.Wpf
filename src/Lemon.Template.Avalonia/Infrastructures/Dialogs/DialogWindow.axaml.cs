using Avalonia.Controls;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Avalonia.Infrastructures.Dialogs
{
    /// <summary>
    /// Default host for <see cref="IDialogService"/> windows.
    /// </summary>
    /// <remarks>
    /// Transient: an Avalonia window cannot be shown again once closed, so every dialog needs a fresh one.
    /// </remarks>
    public partial class DialogWindow : Window, IDialogWindow, ITransientDependency
    {
        public IDialogResult? Result { get; set; }

        public DialogWindow()
        {
            InitializeComponent();
        }
    }
}
