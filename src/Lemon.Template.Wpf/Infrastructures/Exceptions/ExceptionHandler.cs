using Serilog;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Volo.Abp;

namespace Lemon.Template.Wpf.Infrastructures.Exceptions
{
    public class ExceptionHandler
    {
        public void ApplicationExceptionHandler(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Handler(e.Exception);
            e.Handled = true;
        }

        public void DomainExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Handler((Exception)e.ExceptionObject);
            }
            catch
            { }

        }

        public void UnobservedTaskExceptionHandler(object? sender, UnobservedTaskExceptionEventArgs args)
        {
            try
            {
                Handler(args.Exception);
            }
            catch (Exception)
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

        private void Handler(Exception exception)
        {
            exception = Unwrap(exception);

            for (var ex = exception; ex != null; ex = ex.InnerException)
            {
                if (ex is UserFriendlyException userFriendly)
                {
                    Log.Warning(userFriendly, "User-friendly: {Message}", userFriendly.Message);
                    MessageBox.Show(userFriendly.Message, "Application", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (ex is BusinessException business)
                {
                    Log.Warning(business, "Business: {Message}", business.Message);
                    MessageBox.Show(business.Message, "Business Error.", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            Log.Error(exception, "Unhandled: {Message}", exception.Message);
            MessageBox.Show(exception.Message, "Unknown Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}