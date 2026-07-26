using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using Lemon.Template.Wpf.Infrastructures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

// Fixtures for the Views -> ViewModels naming convention. The namespaces are the point, not the types.
namespace Lemon.Template.Wpf.Tests.Views.Fixtures
{
    public class SampleView { }

    public class SamplePage { }

    public class OrphanView { }
}

namespace Lemon.Template.Wpf.Tests.ViewModels.Fixtures
{
    public class SampleViewModel { }

    public class SamplePageViewModel { }
}

namespace Lemon.Template.Wpf.Tests
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
        /// binding in the discarded view against an empty source, which pushes null into target properties
        /// that reject it (WebView2.Source throws NotImplementedException).
        /// </summary>
        [Fact]
        public void ReleaseViewModel_DisposesViewModel_WithoutClearingDataContext()
        {
            RunOnStaThread(() =>
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
            });
        }

        /// <summary>WPF elements must be created on an STA thread; xUnit runs tests on MTA ones.</summary>
        private static void RunOnStaThread(Action action)
        {
            Exception? failure = null;

            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure is not null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
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
