using Lemon.Template.Wpf.Infrastructures.Animations;
using Lemon.Template.Wpf.Infrastructures.Dialogs;
using Lemon.Template.Wpf.Infrastructures.Localization;
using Lemon.Template.Wpf.Infrastructures.Navigations;
using Lemon.Template.Wpf.Themes.Controls;
using Serilog;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Wpf.Views
{

    public partial class MainWindow : Window, ISingletonDependency
    {
        private readonly IHostDialogService _dialog;
        private readonly INavigationService _navigationService;

        public MainWindow(IHostDialogService dialog, INavigationService navigationService)
        {
            InitializeComponent();
            _dialog = dialog;
            _navigationService = navigationService;

            HeaderBorder.MouseDown += (s, e) =>
            {
                if (e.ClickCount == 2) SetWindowState();
            };

            HeaderBorder.MouseMove += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    var window = GetWindow(HeaderBorder);
                    if (window.WindowState == WindowState.Maximized)
                    {
                        // 先计算鼠标在窗口上的相对位置
                        var mousePosition = e.GetPosition(window);
                        var percentHorizontal = mousePosition.X / window.ActualWidth;
                        var targetWidth = window.RestoreBounds.Width;
                        var targetHeight = window.RestoreBounds.Height;

                        // 恢复窗口
                        window.WindowState = WindowState.Normal;

                        // 调整窗口位置，使拖动平滑
                        window.Left = e.GetPosition(null).X - targetWidth * percentHorizontal;
                        window.Top = e.GetPosition(null).Y - mousePosition.Y;
                    }

                    window.DragMove();
                }
            };

            BtnMin.Click += BtnMin_Click;
            BtnMax.Click += BtnMax_Click;
            BtnClose.Click += BtnClose_Click;

            // Checked/Unchecked rather than Click: the toggle's own icon follows IsChecked, and an
            // automation client (or a future binding) can set that without ever raising Click, which
            // would leave the arrow pointing one way and the menu sized the other.
            toggleMenuButton.Checked += (_, _) => SetMenuCollapsed(true);
            toggleMenuButton.Unchecked += (_, _) => SetMenuCollapsed(false);
        }

        public static readonly DependencyProperty IsMenuCollapsedProperty =
            DependencyProperty.Register(
            nameof(IsMenuCollapsed),
            typeof(bool),
            typeof(MainWindow),
            new PropertyMetadata(false));

        public bool IsMenuCollapsed
        {
            get => (bool)GetValue(IsMenuCollapsedProperty);
            set => SetValue(IsMenuCollapsedProperty, value);
        }

        private async void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (await _dialog.Question(LocalizationService.Instance.GetString("Shell_ConfirmExit")))
                {
                    // Shutdown() runs App.OnExit (ABP shutdown, Hangfire stop, Serilog flush).
                    // Environment.Exit would skip all of it and lose buffered log entries.
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                // An async void handler faulting after the first await escapes DispatcherUnhandledException.
                Log.Error(ex, "Close confirmation failed.");
            }
        }

        private void BtnMax_Click(object sender, RoutedEventArgs e)
        {
            SetWindowState();
        }

        private void BtnMin_Click(object sender, RoutedEventArgs e)
        {
            WindowState = (WindowState != WindowState.Minimized) ? WindowState.Minimized : WindowState.Normal;
        }

        private void SetWindowState()
        {
            WindowState = (WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal;
        }

        private const double ExpandedMenuWidth = 240;
        private const double CollapsedMenuWidth = 70;
        private static readonly Duration MenuAnimationDuration = new(TimeSpan.FromMilliseconds(220));

        private void SetMenuCollapsed(bool collapsing)
        {
            // Set first: the item labels fade themselves out off this property (see Navigation.xaml).
            IsMenuCollapsed = collapsing;

            AnimateMenuWidth(collapsing ? CollapsedMenuWidth : ExpandedMenuWidth);
            AnimateHeader(collapsing);
        }

        private void AnimateMenuWidth(double targetWidth)
        {
            var animation = new GridLengthAnimation
            {
                // Reading Width mid-animation yields the current animated value, so repeated toggles
                // pick up where the previous one left off instead of jumping back to the full width.
                From = GridLeftMenu.Width,
                To = new GridLength(targetWidth),
                Duration = MenuAnimationDuration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
            };

            GridLeftMenu.BeginAnimation(ColumnDefinition.WidthProperty, animation);
        }

        private void AnimateHeader(bool collapsing)
        {
            if (!collapsing)
            {
                StackHeader.Visibility = Visibility.Visible;
            }

            var fade = new DoubleAnimation(collapsing ? 0d : 1d, MenuAnimationDuration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
            };

            // Collapse only once faded: hiding it up front would make the title pop out of existence
            // while the column is still sliding.
            fade.Completed += (_, _) => StackHeader.Visibility = collapsing ? Visibility.Collapsed : Visibility.Visible;

            StackHeader.BeginAnimation(OpacityProperty, fade);
        }

        /// <summary>
        /// Handler for <c>controls:TabCloseItem.CloseClick</c>; wire it up when switching the main
        /// region over to the tabbed <see cref="Themes.Controls.TabControl"/> (see MainWindow.xaml).
        /// </summary>
        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TabCloseItem { Content: UserControl view })
            {
                _navigationService.RemoveView(view);
            }
        }
    }
}