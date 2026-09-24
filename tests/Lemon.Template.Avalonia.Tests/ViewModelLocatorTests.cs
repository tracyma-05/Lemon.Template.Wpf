using System;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Lemon.Template.Avalonia.Infrastructures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

// Fixtures for the Views -> ViewModels naming convention. The namespaces are the point, not the types.
namespace Lemon.Template.Avalonia.Tests.Views.Fixtures
{
    public class SampleView { }

    public class SamplePage { }

    public class OrphanView { }
}

namespace Lemon.Template.Avalonia.Tests.ViewModels.Fixtures
{
    public class SampleViewModel { }

    public class SamplePageViewModel { }
}

namespace Lemon.Template.Avalonia.Tests
{
    /// <summary>
    /// The default View -> ViewModel convention wires every page in the app. When it silently stops
    /// matching, views render with a null DataContext and every binding just goes blank.
    /// </summary>
    public class ViewModelLocatorTests
    {
        [Fact]
        public void DefaultConvention_MapsViewSuffixToViewModel()
        {
            var resolved = ViewModelLocator.DefaultViewTypeToViewModel(typeof(Views.Fixtures.SampleView));

            Assert.Equal(typeof(ViewModels.Fixtures.SampleViewModel), resolved);
        }

        [Fact]
        public void DefaultConvention_AppendsViewModelWhenNameDoesNotEndWithView()
        {
            var resolved = ViewModelLocator.DefaultViewTypeToViewModel(typeof(Views.Fixtures.SamplePage));

            Assert.Equal(typeof(ViewModels.Fixtures.SamplePageViewModel), resolved);
        }

        [Fact]
        public void DefaultConvention_ReturnsNull_WhenNoMatchingViewModelExists()
        {
            var resolved = ViewModelLocator.DefaultViewTypeToViewModel(typeof(Views.Fixtures.OrphanView));

            Assert.Null(resolved);
        }

        [Fact]
        public void DefaultConvention_ReturnsNull_WhenTypeIsNotUnderAViewsNamespace()
        {
            var resolved = ViewModelLocator.DefaultViewTypeToViewModel(typeof(ViewModelLocatorTests));

            Assert.Null(resolved);
        }

        /// <summary>
        /// Releasing must dispose the ViewModel but leave DataContext alone: clearing it re-evaluates every
        /// binding in the discarded view against an empty source, which pushes null into target properties.
        /// </summary>
        [AvaloniaFact]
        public void ReleaseViewModel_DisposesViewModel_WithoutClearingDataContext()
        {
            var services = new ServiceCollection()
                .AddScoped<DisposableViewModel>()
                .BuildServiceProvider();

            ViewModelLocator.Register<ReleasableView, DisposableViewModel>();

            var view = new ReleasableView();
            ViewModelLocator.AutoWireViewModel(view, services);
            var viewModel = Assert.IsType<DisposableViewModel>(view.DataContext);

            ViewModelLocator.ReleaseViewModel(view);

            Assert.True(viewModel.Disposed);
            Assert.Same(viewModel, view.DataContext);
        }

        /// <summary>
        /// Avalonia inherits DataContext from the parent, unlike a WPF view the locator sees before it is
        /// attached. A child page must still get its own ViewModel instead of keeping the shell's.
        /// </summary>
        [AvaloniaFact]
        public void AutoWireViewModel_IgnoresInheritedDataContext()
        {
            var services = new ServiceCollection()
                .AddScoped<DisposableViewModel>()
                .BuildServiceProvider();

            ViewModelLocator.Register<ReleasableView, DisposableViewModel>();

            var view = new ReleasableView();
            var host = new Window { DataContext = new object(), Content = view };
            host.Show();

            ViewModelLocator.AutoWireViewModel(view, services);

            Assert.IsType<DisposableViewModel>(view.DataContext);
            host.Close();
        }

        private sealed class ReleasableView : UserControl
        {
        }

        private sealed class DisposableViewModel : IDisposable
        {
            public bool Disposed { get; private set; }

            public void Dispose() => Disposed = true;
        }
    }
}
