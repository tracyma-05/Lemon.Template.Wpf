using Lemon.Template.Wpf.Infrastructures.Dialogs;
using Lemon.Template.Wpf.Infrastructures.Localization;
using Lemon.Template.Wpf.Infrastructures.Navigations;
using Lemon.Template.Wpf.Themes.Controls;
using Serilog;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

            toggleMenuButton.Click += BtnDoubleLeft_Click;
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

        private void BtnDoubleLeft_Click(object sender, RoutedEventArgs e)
        {
            CollapseMenu();
        }

        private void CollapseMenu()
        {
            if (StackHeader.Visibility == Visibility.Visible)
            {
                StackHeader.Visibility = Visibility.Collapsed;
                GridLeftMenu.Width = new GridLength(70);
                IsMenuCollapsed = true;
            }
            else
            {
                StackHeader.Visibility = Visibility.Visible;
                GridLeftMenu.Width = new GridLength(240);
                IsMenuCollapsed = false;
            }
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