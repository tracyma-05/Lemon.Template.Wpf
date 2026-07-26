using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Template.Wpf.Infrastructures.Dialogs;
using Lemon.Template.Wpf.Commons;
using System;
using System.Threading.Tasks;

namespace Lemon.Template.Wpf.Models
{
    /// <summary>
    /// Base class for dialog view models.
    /// </summary>
    /// <remarks>
    /// Implements both dialog contracts so the same view model works with either host:
    /// <see cref="IHostDialogAware"/> for the in-window Material Design <c>DialogHost</c>
    /// (<see cref="IHostDialogService.ShowDialogAsync"/>), and <see cref="IDialogAware"/> for a real
    /// modal window (<see cref="IDialogService.ShowDialog"/> / <see cref="IHostDialogService.ShowWindow"/>).
    /// </remarks>
    public abstract partial class HostDialogViewModel : ViewModelBase, IHostDialogAware, IDialogAware
    {
        /// <summary>Observable here rather than on each derived dialog, so bindings have one source.</summary>
        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _identifierName = Constants.RootIdentifier;

        private readonly IHostDialogService _dialogService;

        public HostDialogViewModel(IHostDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        /// <summary>
        /// Raised to ask a window host to close. Subscribed by <see cref="DialogService"/>; a
        /// <c>DialogHost</c>-hosted dialog leaves it null, which is how <see cref="Close"/> tells the two
        /// hosting modes apart.
        /// </summary>
        public event Action<IDialogResult>? RequestClose;

        /// <summary>Override to veto closing (e.g. unsaved edits).</summary>
        public virtual bool CanCloseDialog() => true;

        public virtual void OnDialogClosed()
        {
        }

        [RelayCommand]
        public virtual void Cancel()
        {
            Close(new DialogResult(ButtonResult.No));
        }

        [RelayCommand]
        public virtual async Task Save()
        {
            Close(new DialogResult(ButtonResult.OK));
            await Task.CompletedTask;
        }

        protected virtual void Save(object value)
        {
            DialogParameters param = new DialogParameters { { "Value", value } };
            Close(new DialogResult(ButtonResult.OK, param));
        }

        protected virtual void Save(DialogParameters param)
        {
            Close(new DialogResult(ButtonResult.OK, param));
        }

        /// <summary>
        /// Closes through whichever host opened this dialog.
        /// </summary>
        protected void Close(DialogResult result)
        {
            var requestClose = RequestClose;
            if (requestClose is not null)
            {
                requestClose(result);
                return;
            }

            _dialogService.Close(IdentifierName, result);
        }

        public abstract void OnDialogOpened(IDialogParameters parameters);
    }
}
