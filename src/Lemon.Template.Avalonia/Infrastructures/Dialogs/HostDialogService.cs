using Avalonia.Controls;
using DialogHostAvalonia;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Avalonia.Infrastructures.Dialogs
{
    public class HostDialogService : DialogService, IHostDialogService, ISingletonDependency
    {
        private readonly IServiceProvider _containerExtension;

        public HostDialogService(IServiceProvider containerExtension)
            : base(containerExtension)
        {
            _containerExtension = containerExtension;
        }

        public async Task<IDialogResult> ShowDialogAsync(string name, IDialogParameters? parameters = null, string IdentifierName = "Root")
        {
            var isDialogOpen = DialogHost.IsDialogOpen(IdentifierName);
            if (isDialogOpen) return new DialogResult(ButtonResult.Ignore);

            var dialogContent = GetDialogContent(name, IdentifierName);

            if (!(dialogContent.DataContext is IHostDialogAware viewModel))
                throw new InvalidOperationException("A dialog's ViewModel must implement the IHostDialogAware interface");

            var eventHandler = GetDialogOpenedEventHandler(viewModel, parameters);

            var dialogResult = await DialogHost.Show(dialogContent, IdentifierName, eventHandler);

            if (dialogResult == null)
                return new DialogResult(ButtonResult.Cancel);

            return (IDialogResult)dialogResult;
        }

        private Control GetDialogContent(string name, string IdentifierName = "Root")
        {
            var dialogContent = _containerExtension.GetKeyedService<UserControl>(name)
                                ?? throw new InvalidOperationException($"No dialog view is registered under '{name}'.");

            ViewModelLocator.AutoWireViewModel(dialogContent, _containerExtension);

            if (!(dialogContent.DataContext is IHostDialogAware viewModel))
                throw new InvalidOperationException("A dialog's ViewModel must implement the IHostDialogAware interface");

            viewModel.IdentifierName = IdentifierName;

            return dialogContent;
        }

        private static DialogOpenedEventHandler GetDialogOpenedEventHandler(IHostDialogAware viewModel,
            IDialogParameters? parameters)
        {
            parameters ??= new DialogParameters();

            return (_, _) => viewModel.OnDialogOpened(parameters);
        }

        public void Close(string IdentifierName, DialogResult dialogResult)
        {
            DialogHost.Close(IdentifierName, dialogResult);
        }
    }
}
