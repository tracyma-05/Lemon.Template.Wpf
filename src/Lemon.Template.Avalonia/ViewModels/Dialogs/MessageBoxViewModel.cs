using CommunityToolkit.Mvvm.ComponentModel;
using Lemon.Template.Avalonia.Infrastructures.Dialogs;
using Lemon.Template.Avalonia.Models;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Avalonia.ViewModels.Dialogs
{
    public partial class MessageBoxViewModel : HostDialogViewModel, ITransientDependency
    {
        [ObservableProperty]
        private string _message = string.Empty;

        public MessageBoxViewModel(IHostDialogService dialogService)
            : base(dialogService)
        {
        }

        public override void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.ContainsKey("Title"))
                Title = parameters.GetValue<string>("Title");

            if (parameters.ContainsKey("Message"))
                Message = parameters.GetValue<string>("Message");
        }
    }
}