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

        /// <summary>持有自动装配 ViewModel 的 DI 作用域，便于确定性释放。</summary>
        private static readonly DependencyProperty OwnedViewModelScopeProperty =
            DependencyProperty.RegisterAttached(
                "OwnedViewModelScope",
                typeof(IServiceScope),
                typeof(ViewModelLocator),
                new PropertyMetadata(null));

        // 默认约定规则：Views 命名空间 -> ViewModels 命名空间
        private static Func<Type, Type?> _defaultViewTypeToViewModelTypeResolver = DefaultViewTypeToViewModel;

        private static bool _autoWiringEnabled;

        /// <summary>
        /// 注册全局 <see cref="FrameworkElement.LoadedEvent"/> 类处理器，为每个加载的 View 自动装配 ViewModel。
        /// </summary>
        /// <remarks>
        /// 必须在 DI 容器就绪后调用：类处理器对进程内所有 <see cref="FrameworkElement"/> 生效，过早注册会让
        /// 启动阶段加载的元素（启动画面、调试器注入的 XAML 热重载适配器）去取尚不存在的服务。
        /// 容器只按需读取——<c>ApplicationInitializationContext.ServiceProvider</c> 是初始化期作用域，
        /// ABP 在启动结束后即释放，捕获它会让后续每次导航都失败。
        /// </remarks>
        public static void EnableAutoWiring()
        {
            if (_autoWiringEnabled) return;
            _autoWiringEnabled = true;

            EventManager.RegisterClassHandler(
                typeof(FrameworkElement),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler((sender, _) =>
                {
                    if (sender is FrameworkElement view && App.ServiceProviderOrNull is { } serviceProvider)
                    {
                        AutoWireViewModel(view, serviceProvider);
                    }
                }));
        }

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
        /// 自动解析并注入 ViewModel。已有 DataContext 的 View 会被跳过，因此重复调用是安全的。
        /// </summary>
        public static void AutoWireViewModel(FrameworkElement view, IServiceProvider serviceProvider)
        {
            if (view == null) return;
            if (view.DataContext != null) return;

            var viewModelType = ResolveViewModelType(view.GetType());
            if (viewModelType == null) return;

            var scope = serviceProvider.CreateScope();
            object? viewModel;
            try
            {
                viewModel = scope.ServiceProvider.GetService(viewModelType)
                            ?? Activator.CreateInstance(viewModelType);
            }
            catch
            {
                scope.Dispose();
                throw;
            }

            if (viewModel == null)
            {
                scope.Dispose();
                return;
            }

            view.DataContext = viewModel;
            view.SetValue(OwnedViewModelScopeProperty, scope);
        }

        /// <summary>
        /// 释放由 <see cref="AutoWireViewModel"/> 装配的 ViewModel：先释放 ViewModel（若实现
        /// <see cref="IDisposable"/>），再释放创建它的 DI 作用域。
        /// </summary>
        /// <remarks>
        /// 这里刻意不使用 <see cref="FrameworkElement.Unloaded"/>：WPF 在 View 只是临时脱离可视树时
        /// （切换选项卡、模板重建）同样会触发该事件，绑定到它会把仍在使用中的 ViewModel 提前拆掉。
        /// 释放时机由导航层（<see cref="Navigations.INavigationService.RemoveView"/> 及区域内容替换）决定。
        /// </remarks>
        public static void ReleaseViewModel(FrameworkElement view)
        {
            if (view == null) return;
            if (view.GetValue(OwnedViewModelScopeProperty) is not IServiceScope scope) return;

            view.SetValue(OwnedViewModelScopeProperty, null);

            // 刻意不清空 DataContext：View 此时已被移出可视树并丢弃，置空并不能多回收什么，
            // 却会让整棵子树的绑定以空源重新求值，把 null 推回目标属性；部分控件拒绝 null
            // （如 WebView2.Source 会抛 NotImplementedException）。
            var viewModel = view.DataContext;

            try
            {
                if (viewModel is IDisposable disposableViewModel)
                {
                    disposableViewModel.Dispose();
                }
            }
            finally
            {
                scope.Dispose();
            }
        }

        private static Type? ResolveViewModelType(Type viewType)
        {
            if (_viewModelMappings.TryGetValue(viewType.FullName!, out var mapped))
            {
                return mapped;
            }

            return _defaultViewTypeToViewModelTypeResolver(viewType);
        }

        /// <summary>
        /// 默认约定规则：Views 命名空间 -> ViewModels 命名空间。
        /// internal 而非 private，以便单元测试直接覆盖这条约定。
        /// </summary>
        internal static Type? DefaultViewTypeToViewModel(Type viewType)
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
