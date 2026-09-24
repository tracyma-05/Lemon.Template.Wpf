using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Avalonia.Infrastructures.Dialogs
{
    public class DialogService : IDialogService, ISingletonDependency
    {
        private readonly IServiceProvider _containerExtension;

        /// <summary>
        /// Initializes a new instance of the <see cref="DialogService"/> class.
        /// </summary>
        /// <param name="containerExtension"></param>
        public DialogService(IServiceProvider containerExtension)
        {
            _containerExtension = containerExtension;
        }

        /// <summary>
        /// Shows a non-modal dialog.
        /// </summary>
        /// <param name="name">The name of the dialog to show.</param>
        /// <param name="parameters">The parameters to pass to the dialog.</param>
        /// <param name="callback">The action to perform when the dialog is closed.</param>
        public void Show(string name, IDialogParameters? parameters, Action<IDialogResult>? callback)
        {
            Show(name, parameters, callback, windowName: null);
        }

        /// <summary>
        /// Shows a non-modal dialog.
        /// </summary>
        /// <param name="name">The name of the dialog to show.</param>
        /// <param name="parameters">The parameters to pass to the dialog.</param>
        /// <param name="callback">The action to perform when the dialog is closed.</param>
        /// <param name="windowName">The key of the hosting window registered in the container.</param>
        public void Show(string name, IDialogParameters? parameters, Action<IDialogResult>? callback, string? windowName)
        {
            // Non-modal: showing returns immediately, so there is nothing to await here.
            _ = ShowDialogInternalAsync(name, parameters, callback, isModal: false, windowName);
        }

        /// <inheritdoc />
        public async Task<IDialogResult> ShowWindowAsync(string name, IDialogParameters? parameters = null, string? windowName = null)
        {
            IDialogResult dialogResult = new DialogResult(ButtonResult.None);

            await ShowDialogInternalAsync(name, parameters, result => dialogResult = result, isModal: true, windowName);

            return dialogResult;
        }

        private Task ShowDialogInternalAsync(string name, IDialogParameters? parameters, Action<IDialogResult>? callback, bool isModal, string? windowName)
        {
            parameters ??= new DialogParameters();

            IDialogWindow dialogWindow = CreateDialogWindow(windowName);
            ConfigureDialogWindowEvents(dialogWindow, callback);
            ConfigureDialogWindowContent(name, dialogWindow, parameters);

            return ShowDialogWindowAsync(dialogWindow, isModal);
        }

        /// <summary>
        /// Shows the dialog window.
        /// </summary>
        /// <param name="dialogWindow">The dialog window to show.</param>
        /// <param name="isModal">If true; dialog is shown as a modal</param>
        /// <returns>For a modal dialog, completes when the window has closed.</returns>
        protected virtual Task ShowDialogWindowAsync(IDialogWindow dialogWindow, bool isModal)
        {
            if (!isModal)
            {
                dialogWindow.Show();
                return Task.CompletedTask;
            }

            var owner = FindOwnerWindow();
            if (owner is not null)
            {
                return dialogWindow.ShowDialog(owner);
            }

            // No window to be modal to (e.g. called before the shell is up): still let the caller await.
            var closed = new TaskCompletionSource();
            dialogWindow.Closed += (_, _) => closed.TrySetResult();
            dialogWindow.Show();
            return closed.Task;
        }

        /// <summary>
        /// Create a new <see cref="IDialogWindow"/>.
        /// </summary>
        /// <param name="name">The key of the hosting window registered in the container.</param>
        /// <returns>The created <see cref="IDialogWindow"/>.</returns>
        protected virtual IDialogWindow CreateDialogWindow(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return _containerExtension.GetRequiredService<IDialogWindow>();
            else
                return _containerExtension.GetRequiredKeyedService<IDialogWindow>(name);
        }

        /// <summary>
        /// Configure <see cref="IDialogWindow"/> content.
        /// </summary>
        /// <param name="dialogName">The name of the dialog to show.</param>
        /// <param name="window">The hosting window.</param>
        /// <param name="parameters">The parameters to pass to the dialog.</param>
        protected virtual void ConfigureDialogWindowContent(string dialogName, IDialogWindow window, IDialogParameters parameters)
        {
            // Keyed as UserControl, matching the [KeyedService] registrations on the dialog views.
            var dialogContent = _containerExtension.GetRequiredKeyedService<UserControl>(dialogName);

            ViewModelLocator.AutoWireViewModel(dialogContent, _containerExtension);

            if (!(dialogContent.DataContext is IDialogAware viewModel))
                throw new InvalidOperationException("A dialog's ViewModel must implement the IDialogAware interface");

            ConfigureDialogWindowProperties(window, dialogContent, viewModel);

            ViewModelLocator.ViewAndViewModelAction<IDialogAware>(viewModel, d => d.OnDialogOpened(parameters));
        }

        /// <summary>
        /// Configure <see cref="IDialogWindow"/> and <see cref="IDialogAware"/> events.
        /// </summary>
        /// <param name="dialogWindow">The hosting window.</param>
        /// <param name="callback">The action to perform when the dialog is closed.</param>
        protected virtual void ConfigureDialogWindowEvents(IDialogWindow dialogWindow, Action<IDialogResult>? callback)
        {
            void RequestCloseHandler(IDialogResult result)
            {
                dialogWindow.Result = result;
                dialogWindow.Close();
            }

            void ClosingHandler(object? sender, WindowClosingEventArgs e)
            {
                if (!dialogWindow.GetDialogViewModel().CanCloseDialog())
                    e.Cancel = true;
            }

            // Declared before assignment so each handler can unsubscribe itself.
            EventHandler<RoutedEventArgs>? loadedHandler = null;
            loadedHandler = (o, e) =>
            {
                dialogWindow.Loaded -= loadedHandler;
                dialogWindow.GetDialogViewModel().RequestClose += RequestCloseHandler;
            };
            dialogWindow.Loaded += loadedHandler;

            dialogWindow.Closing += ClosingHandler;

            EventHandler? closedHandler = null;
            closedHandler = (o, e) =>
            {
                dialogWindow.Closed -= closedHandler;
                dialogWindow.Closing -= ClosingHandler;
                dialogWindow.GetDialogViewModel().RequestClose -= RequestCloseHandler;

                dialogWindow.GetDialogViewModel().OnDialogClosed();

                dialogWindow.Result ??= new DialogResult();
                try
                {
                    callback?.Invoke(dialogWindow.Result);
                }
                catch (Exception ex)
                {
                    // Raised from the window's Closed event: an exception here would surface far from its cause.
                    Log.Error(ex, "Dialog close callback failed.");
                }

                dialogWindow.DataContext = null;
                dialogWindow.Content = null;
            };
            dialogWindow.Closed += closedHandler;
        }

        /// <summary>
        /// Configure <see cref="IDialogWindow"/> properties.
        /// </summary>
        /// <param name="window">The hosting window.</param>
        /// <param name="dialogContent">The dialog to show.</param>
        /// <param name="viewModel">The dialog's ViewModel.</param>
        protected virtual void ConfigureDialogWindowProperties(IDialogWindow window, Control dialogContent, IDialogAware viewModel)
        {
            window.Content = dialogContent;
            window.DataContext = viewModel; //we want the host window and the dialog to share the same data context
        }

        /// <summary>The active window, falling back to the main window.</summary>
        private static Window? FindOwnerWindow()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return null;

            return desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow;
        }
    }
}
