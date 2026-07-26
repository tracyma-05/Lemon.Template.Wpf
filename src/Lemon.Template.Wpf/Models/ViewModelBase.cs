using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Threading.Tasks;

namespace Lemon.Template.Wpf.Models
{
    /// <summary>
    /// Inherits <see cref="ObservableObject"/> so derived view models can use [ObservableProperty]
    /// without each of them applying [ObservableObject] (MVVMTK0033).
    /// </summary>
    public partial class ViewModelBase : ObservableObject
    {
        public bool IsBusy { get; set; }

        public virtual async Task SetBusyAsync(Func<Task> func, string? loadingMessage = null)
        {
            IsBusy = true;
            try
            {
                await func();
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
