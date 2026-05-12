using System;
using System.Threading.Tasks;

namespace Lemon.Template.Wpf.Models
{
    
    public partial class ViewModelBase
    {
        public bool IsBusy { get; set; }

        public virtual async Task SetBusyAsync(Func<Task> func, string loadingMessage = null)
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
