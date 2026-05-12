using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Windows;

namespace Lemon.Template.Wpf.Infrastructures
{
    public static class ViewModelLocator
    {
        private static readonly Dictionary<string, Type> _viewModelMappings = new();

        // 默认约定规则：Views 命名空间 -> ViewModels 命名空间
        private static Func<Type, Type?> _defaultViewTypeToViewModelTypeResolver = DefaultViewTypeToViewModel;

        /// <summary>
        /// 设置默认约定解析规则
        /// </summary>
        public static void SetDefaultResolver(Func<Type, Type?> resolver)
        {
            _defaultViewTypeToViewModelTypeResolver = resolver;
        }

        /// <summary>
        /// 手动注册 View-ViewModel 映射
        /// </summary>
        public static void Register<TView, TViewModel>()
        {
            _viewModelMappings[typeof(TView).FullName!] = typeof(TViewModel);
        }

        /// <summary>
        /// 自动解析并注入 ViewModel
        /// </summary>
        public static void AutoWireViewModel(FrameworkElement view, IServiceProvider serviceProvider)
        {
            if (view == null) return;
            if (view.DataContext != null) return;

            var viewType = view.GetType();
            Type? viewModelType = null;

            if (!_viewModelMappings.TryGetValue(viewType.FullName!, out viewModelType))
            {
                viewModelType = _defaultViewTypeToViewModelTypeResolver(viewType);
            }

            if (viewModelType == null) return;

            var scope = serviceProvider.CreateScope();
            var viewModel = scope.ServiceProvider.GetService(viewModelType)
                            ?? Activator.CreateInstance(viewModelType);

            view.DataContext = viewModel;
            view.Unloaded += (s, e) =>
            {
                scope.Dispose();
                if (view is IDisposable disposableVm)
                {
                    disposableVm.Dispose();
                }
            };
        }

        /// <summary>
        /// 默认约定规则：Views 命名空间 -> ViewModels 命名空间
        /// </summary>
        private static Type? DefaultViewTypeToViewModel(Type viewType)
        {
            var viewName = viewType.FullName;
            if (viewName == null) return null;

            viewName = viewName.Replace(".Views.", ".ViewModels.");
            var asmName = viewType.GetTypeInfo().Assembly.FullName;

            var suffix = viewName.EndsWith("View", StringComparison.Ordinal) ? "Model" : "ViewModel";
            var viewModelName = string.Format(CultureInfo.InvariantCulture, "{0}{1}, {2}", viewName, suffix, asmName);

            return Type.GetType(viewModelName);
        }

        public static void ViewAndViewModelAction<T>(object view, Action<T> action) where T : class
        {
            if (view is T viewAsT)
                action(viewAsT);

            if (view is FrameworkElement element && element.DataContext is T viewModelAsT)
            {
                action(viewModelAsT);
            }
        }
    }
}