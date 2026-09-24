using Avalonia.Threading;
using Serilog;
using System;
using System.Threading.Tasks;
using Volo.Abp;

namespace Lemon.Template.Avalonia.Infrastructures.Exceptions
{
    public class ExceptionHandler
    {
        /// <summary>UI 线程独占：避免一次故障风暴叠出一堆模态框。</summary>
        private bool _dialogVisible;

        public void ApplicationExceptionHandler(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Handle(e.Exception, showDialog: true);
            e.Handled = true;
        }

        public void DomainExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var exception = e.ExceptionObject as Exception
                                ?? new InvalidOperationException($"Non-CLR exception: {e.ExceptionObject}");

                // 进程即将终止时才打扰用户；否则只留日志。
                Handle(exception, showDialog: e.IsTerminating);
            }
            catch
            { }
        }

        public void UnobservedTaskExceptionHandler(object? sender, UnobservedTaskExceptionEventArgs args)
        {
            try
            {
                // 后台任务故障只记日志：它未必影响用户当前操作，弹模态框反而会打断。
                Handle(args.Exception, showDialog: false);
                args.SetObserved();
            }
            catch
            { }
        }

        private static Exception Unwrap(Exception exception)
        {
            if (exception is AggregateException aggregate)
            {
                var flat = aggregate.Flatten();
                if (flat.InnerExceptions.Count == 1)
                    return flat.InnerExceptions[0];
            }

            return exception;
        }

        private void Handle(Exception exception, bool showDialog)
        {
            exception = Unwrap(exception);

            for (var ex = exception; ex != null; ex = ex.InnerException)
            {
                if (ex is UserFriendlyException userFriendly)
                {
                    Log.Warning(userFriendly, "User-friendly: {Message}", userFriendly.Message);
                    if (showDialog)
                        ShowDialog(userFriendly.Message, "Application", isWarning: true);
                    return;
                }

                if (ex is BusinessException business)
                {
                    Log.Warning(business, "Business: {Message}", business.Message);
                    if (showDialog)
                        ShowDialog(business.Message, "Business Error.", isWarning: true);
                    return;
                }
            }

            Log.Error(exception, "Unhandled: {Message}", exception.Message);
            if (showDialog)
                ShowDialog(exception.Message, "Unknown Error", isWarning: false);
        }

        /// <summary>
        /// 窗口必须在 UI 线程创建，而域异常和未观察任务异常来自任意线程。
        /// </summary>
        private void ShowDialog(string message, string caption, bool isWarning)
        {
            var dispatcher = Dispatcher.UIThread;

            if (dispatcher.CheckAccess())
            {
                _ = ShowDialogCoreAsync(message, caption, isWarning);
            }
            else
            {
                dispatcher.Post(() => _ = ShowDialogCoreAsync(message, caption, isWarning));
            }
        }

        private async Task ShowDialogCoreAsync(string message, string caption, bool isWarning)
        {
            if (_dialogVisible)
            {
                return;
            }

            _dialogVisible = true;
            try
            {
                await ErrorWindow.ShowAsync(message, caption, isWarning);
            }
            catch (Exception ex)
            {
                // Showing the report must never become a second unhandled exception.
                Log.Warning(ex, "Could not show the error window.");
            }
            finally
            {
                _dialogVisible = false;
            }
        }
    }
}
