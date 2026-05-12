using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Template.Wpf.Infrastructures.Dialogs;
using Lemon.Template.Wpf.Infrastructures.Navigations;

namespace Lemon.Template.Wpf.Models
{
    public partial class NavigationViewModel : ObservableObject, INavigationAware
    {
        [ObservableProperty]
        private string _title;

        public IHostDialogService HostDialogService { get; private set; }

        public NavigationViewModel(IHostDialogService hostDialogService)
        {
            HostDialogService = hostDialogService;
        }

        [RelayCommand]
        public virtual void Refresh() { }

        public virtual void OnNavigatedFrom(NavigationContext navigationContext) { }

        public virtual void OnNavigatedTo(NavigationContext navigationContext) { }
    }
}
