using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Wpf.Infrastructures.Dialogs
{
    public class HostDialogService : DialogService, IHostDialogService, ISingletonDependency
    {
        private readonly IServiceProvider _containerExtension;

        public HostDialogService(IServiceProvider containerExtension)
            : base(containerExtension)
        {
            _containerExtension = containerExtension;
        }

        public IDialogResult ShowWindow(string name)
        {
            IDialogResult dialogResult = new DialogResult(ButtonResult.None);

            var content = _containerExtension.GetKeyedServices<UserControl>(name);

            if (!(content is Window dialogContent))
                throw new NullReferenceException("A dialog's content must be a Window");

            if (dialogContent is Window view && view.DataContext is null)
                ViewModelLocator.AutoWireViewModel(view, _containerExtension);

            if (!(dialogContent.DataContext is IDialogAware viewModel))
                throw new NullReferenceException("A dialog's ViewModel must implement the IDialogAware interface");

            if (dialogContent is IDialogWindow dialogWindow)
            {
                ConfigureDialogWindowEvents(dialogWindow, result => { dialogResult = result; });
            }

            ViewModelLocator.ViewAndViewModelAction<IDialogAware>(viewModel, d => d.OnDialogOpened(null));
            dialogContent.ShowDialog();
            return dialogResult;
        }

        public async Task<IDialogResult> ShowDialogAsync(string name, IDialogParameters parameters = null, string IdentifierName = "Root")
        {
            var dialogContent = GetDialogContent(name, IdentifierName);

            if (!(dialogContent.DataContext is IHostDialogAware viewModel))
                throw new NullReferenceException("A dialog's ViewModel must implement the IDialogHostAware interface");

            var eventHandler = GetDialogOpenedEventHandler(viewModel, parameters);

            var isDialogOpen = DialogHost.IsDialogOpen(IdentifierName);
            if (isDialogOpen) return new DialogResult(ButtonResult.Ignore);

            var dialogResult = await DialogHost.Show(dialogContent, IdentifierName, eventHandler);

            if (dialogResult == null)
                return new DialogResult(ButtonResult.Cancel);

            return (IDialogResult)dialogResult;
        }

        private FrameworkElement GetDialogContent(string name, string IdentifierName = "Root")
        {
            var content = _containerExtension.GetKeyedService<UserControl>(name);
            if (!(content is FrameworkElement dialogContent))
                throw new NullReferenceException("A dialog's content must be a FrameworkElement");

            if (dialogContent is FrameworkElement view && view.DataContext is null)
                ViewModelLocator.AutoWireViewModel(view, _containerExtension);

            if (!(dialogContent.DataContext is IHostDialogAware viewModel))
                throw new NullReferenceException("A dialog's ViewModel must implement the IDialogHostAware interface");

            viewModel.IdentifierName = IdentifierName;

            return dialogContent;
        }

        private DialogOpenedEventHandler GetDialogOpenedEventHandler(IHostDialogAware viewModel,
            IDialogParameters parameters)
        {
            if (parameters == null) parameters = new DialogParameters();

            DialogOpenedEventHandler eventHandler =
               (sender, eventArgs) =>
               {
                   var _content = eventArgs.Session.Content;
                   if (viewModel is IHostDialogAware aware)
                       aware.OnDialogOpened(parameters);
                   eventArgs.Session.UpdateContent(_content);
               };

            return eventHandler;
        }

        public void Close(string IdentifierName, DialogResult dialogResult)
        {
            DialogHost.Close(IdentifierName, dialogResult);
        }
    }
}