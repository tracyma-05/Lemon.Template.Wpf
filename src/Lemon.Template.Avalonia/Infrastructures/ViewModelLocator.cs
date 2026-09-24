using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Lemon.Template.Avalonia.Infrastructures
{
    public static class ViewModelLocator
    {
        private static readonly Dictionary<string, Type> _viewModelMappings = new();

        /// <summary>持有自动装配 ViewModel 的 DI 作用域，便于确定性释放。</summary>
        private static readonly AttachedProperty<IServiceScope?> OwnedViewModelScopeProperty =
            AvaloniaProperty.RegisterAttached<Control, IServiceScope?>("OwnedViewModelScope", typeof(ViewModelLocator));

        // 默认约定规则：Views 命名空间 -> ViewModels 命名空间
        private static Func<Type, Type?> _defaultViewTypeToViewModelTypeResolver = DefaultViewTypeToViewModel;

        private static bool _autoWiringEnabled;

        /// <summary>
        /// 注册全局 <see cref="Control.LoadedEvent"/> 类处理器，为每个加载的 View 自动装配 ViewModel。
        /// </summary>
        /// <remarks>
        /// 必须在 DI 容器就绪后调用：类处理器对进程内所有 <see cref="Control"/> 生效，过早注册会让
        /// 启动阶段加载的控件（启动画面）去取尚不存在的服务。
        /// 容器只按需读取——<c>ApplicationInitializationContext.ServiceProvider</c> 是初始化期作用域，
        /// ABP 在启动结束后即释放，捕获它会让后续每次导航都失败。
        /// </remarks>
        public static void EnableAutoWiring()
        {
            if (_autoWiringEnabled) return;
            _autoWiringEnabled = true;

            Control.LoadedEvent.AddClassHandler<Control>((view, _) =>
            {
                if (App.ServiceProviderOrNull is { } serviceProvider)
                {
                    AutoWireViewModel(view, serviceProvider);
                }
            }, RoutingStrategies.Direct);
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
        /// <remarks>
        /// 只看本地值：Avalonia 的 DataContext 会从父级继承，读取合并后的值会把继承来的父级
        /// ViewModel 误当成“已装配”，导致子 View 永远拿不到自己的 ViewModel。
        /// </remarks>
        public static void AutoWireViewModel(Control view, IServiceProvider serviceProvider)
        {
            if (view == null) return;
            if (view.IsSet(StyledElement.DataContextProperty)) return;

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
        /// 这里刻意不使用 <see cref="Control.Unloaded"/>：View 只是临时脱离可视树时
        /// （切换选项卡、模板重建）同样会触发该事件，绑定到它会把仍在使用中的 ViewModel 提前拆掉。
        /// 释放时机由导航层（<see cref="Navigations.INavigationService.RemoveView"/> 及区域内容替换）决定。
        /// </remarks>
        public static void ReleaseViewModel(Control view)
        {
            if (view == null) return;
            if (view.GetValue(OwnedViewModelScopeProperty) is not IServiceScope scope) return;

            view.SetValue(OwnedViewModelScopeProperty, null);

            // 刻意不清空 DataContext：View 此时已被移出可视树并丢弃，置空并不能多回收什么，
            // 却会让整棵子树的绑定以空源重新求值，把 null 推回目标属性。
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

            // Loaded 类处理器会对每个控件（包括模板内部件）调用，其中绝大多数都不是 View：
            // 在这里提前返回，可省去每个控件一次 Type.GetType 查找。
            if (!viewName.Contains(".Views.", StringComparison.Ordinal)) return null;

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

            if (view is StyledElement element && element.DataContext is T viewModelAsT)
            {
                action(viewModelAsT);
            }
        }
    }
}
