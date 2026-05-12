using CommunityToolkit.Mvvm.Input;
using Lemon.Template.Wpf.Infrastructures.Dialogs;
using System.Threading.Tasks;

namespace Lemon.Template.Wpf.Models
{
    public abstract partial class HostDialogViewModel : ViewModelBase, IHostDialogAware
    {
        public string Title { get; set; }

        public string IdentifierName { get; set; }

        private IHostDialogService _dialogService;

        public HostDialogViewModel(IHostDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        [RelayCommand]
        public virtual void Cancel()
        {
            _dialogService.Close(IdentifierName, new DialogResult(ButtonResult.No));
        }

        [RelayCommand]
        public virtual async Task Save()
        {
            _dialogService.Close(IdentifierName, new DialogResult(ButtonResult.OK));
            await Task.CompletedTask;
        }

        protected virtual void Save(object value)
        {
            DialogParameters param = new DialogParameters { { "Value", value } };
            _dialogService.Close(IdentifierName, new DialogResult(ButtonResult.OK, param));
        }

        protected virtual void Save(DialogParameters param)
        {
            _dialogService.Close(IdentifierName, new DialogResult(ButtonResult.OK, param));
        }

        public abstract void OnDialogOpened(IDialogParameters parameters);
    }
}