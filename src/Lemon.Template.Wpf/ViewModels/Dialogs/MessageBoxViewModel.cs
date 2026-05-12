using CommunityToolkit.Mvvm.ComponentModel;
using Lemon.Template.Wpf.Infrastructures.Dialogs;
using Lemon.Template.Wpf.Models;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Wpf.ViewModels.Dialogs
{
    [ObservableObject]
    public partial class MessageBoxViewModel : HostDialogViewModel, ITransientDependency
    {
        [ObservableProperty]
        private string _message;

        [ObservableProperty]
        private string _title;

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