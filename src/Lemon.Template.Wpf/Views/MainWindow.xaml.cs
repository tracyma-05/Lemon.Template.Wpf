using Lemon.Template.Wpf.Infrastructures.Animations;
using Lemon.Template.Wpf.Infrastructures.Dialogs;
using Lemon.Template.Wpf.Infrastructures.Localization;
using Lemon.Template.Wpf.Infrastructures.Navigations;
#if (EnableTrayIcon)
using Lemon.Template.Wpf.Infrastructures.Shell;
#endif
using Lemon.Template.Wpf.Themes.Controls;
using Lemon.Template.Wpf.ViewModels;
using Serilog;
using System;
#if (EnableTrayIcon)
using System.ComponentModel;
#endif
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
#if (EnableTrayIcon)
using System.Windows.Interop;
#endif
using System.Windows.Media.Animation;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Wpf.Views
{

    public partial class MainWindow : Window, ISingletonDependency
    {
        private readonly IHostDialogService _dialog;
        private readonly INavigationService _navigationService;
#if (EnableTrayIcon)
        private readonly AppTrayIcon _trayIcon;

        // What to return to when shown from the tray or restored from the taskbar: setting Normal blindly would
        // turn a window that was maximized before it was minimized into a small one.
        private WindowState _restoreState = WindowState.Normal;
#endif

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

            ContentRendered += OnFirstContentRendered;
#if (EnableTrayIcon)

            _trayIcon = new AppTrayIcon(open: ShowFromTray, exit: App.Quit);
            StateChanged += (_, _) =>
            {
                if (WindowState != WindowState.Minimized)
                {
                    _restoreState = WindowState;
                }
            };
#endif
        }
#if (EnableTrayIcon)

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // A second launch posts this message (see SingleInstance) and exits; this instance shows itself instead.
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(OnWindowMessage);
        }

        private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if ((uint)msg == SingleInstance.ShowExistingMessage)
            {
                ShowFromTray();
                handled = true;
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Close hides the window in the tray; only <see cref="App.Quit"/> (the tray's Exit command, an update
        /// restart) or Windows signing out really closes it. Intercepted here rather than on the title-bar button
        /// because Alt+F4, the taskbar's "Close window" and the system menu all arrive through OnClosing too.
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            if (!App.IsExiting)
            {
                e.Cancel = true;
                // Hide also removes the taskbar button, so ShowInTaskbar needs no change.
                Hide();
                _trayIcon.ShowHiddenHintOnce();
                return;
            }

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _trayIcon.Dispose();
            base.OnClosed(e);
        }

        private void ShowFromTray()
        {
            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = _restoreState;
            }

            // Activate alone is sometimes refused while another app is in the foreground; a Topmost toggle is not.
            Topmost = true;
            Topmost = false;
            Activate();
        }
#endif

        /// <summary>
        /// Start-up update check. Waits for the first render so the splash is gone and the dialog host
        /// exists before any "new version" prompt can appear.
        /// </summary>
        private async void OnFirstContentRendered(object? sender, EventArgs e)
        {
            ContentRendered -= OnFirstContentRendered;

            if (DataContext is MainWindowViewModel viewModel)
            {
                // Handles its own failures; see CheckForUpdatesOnStartupAsync.
                await viewModel.CheckForUpdatesOnStartupAsync();
            }
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

#if (EnableTrayIcon)
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            // Hides in the tray (see OnClosing); quitting is the tray menu's Exit command.
            Close();
        }
#else
        private async void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (await _dialog.Question(LocalizationService.Instance.GetString("Shell_ConfirmExit")))
                {
                    App.Quit();
                }
            }
            catch (Exception ex)
            {
                // An async void handler faulting after the first await escapes DispatcherUnhandledException.
                Log.Error(ex, "Close confirmation failed.");
            }
        }
#endif

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