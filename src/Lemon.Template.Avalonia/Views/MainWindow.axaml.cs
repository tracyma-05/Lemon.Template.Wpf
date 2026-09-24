using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Lemon.Template.Avalonia.Infrastructures.Dialogs;
using Lemon.Template.Avalonia.Infrastructures.Localization;
using Lemon.Template.Avalonia.Infrastructures.Navigations;
using Lemon.Template.Avalonia.Themes.Controls;
using Serilog;
using System;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Avalonia.Views
{
    public partial class MainWindow : Window, ISingletonDependency
    {
        private const double ExpandedMenuWidth = 240;
        private const double CollapsedMenuWidth = 70;

        /// <summary>Room left for the macOS traffic lights, which float over the side-menu header.</summary>
        private const double MacTrafficLightsWidth = 72;

        private readonly IHostDialogService _dialog;
        private readonly INavigationService _navigationService;

        /// <summary>Set once the user has confirmed, so the second <see cref="Close()"/> goes through.</summary>
        private bool _closeConfirmed;

        public static readonly StyledProperty<bool> IsMenuCollapsedProperty =
            AvaloniaProperty.Register<MainWindow, bool>(nameof(IsMenuCollapsed));

        public MainWindow(IHostDialogService dialog, INavigationService navigationService)
        {
            _dialog = dialog;
            _navigationService = navigationService;

            InitializeComponent();

            ConfigureCaptionButtons();

            // IsCheckedChanged rather than Click: an automation client (or a future binding) can set
            // IsChecked without ever raising Click, which would leave the arrow pointing one way and the
            // menu sized the other.
            ToggleMenuButton.IsCheckedChanged += (_, _) => SetMenuCollapsed(ToggleMenuButton.IsChecked == true);
        }

        public bool IsMenuCollapsed
        {
            get => GetValue(IsMenuCollapsedProperty);
            set => SetValue(IsMenuCollapsedProperty, value);
        }

        /// <summary>
        /// Windows uses the three caption buttons in MainWindow.axaml. macOS keeps its native traffic lights,
        /// which float over the top left of the side menu, so ours are hidden and the menu header moves right.
        /// </summary>
        /// <remarks>
        /// Dragging and double-click-to-maximise need no code: the title bar strip carries the TitleBar element
        /// role, and the OS does both (plus snapping) itself.
        /// </remarks>
        private void ConfigureCaptionButtons()
        {
            if (OperatingSystem.IsMacOS())
            {
                CaptionButtons.IsVisible = false;
                var m = MenuHeader.Margin;
                MenuHeader.Margin = new Thickness(MacTrafficLightsWidth, m.Top, m.Right, m.Bottom);
                return;
            }

            MinimizeButton.Click += (_, _) => WindowState = WindowState.Minimized;
            MaximizeButton.Click += (_, _) =>
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            // Close() goes through OnClosing, so the exit confirmation below applies to this button too.
            CloseButton.Click += (_, _) => Close();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            // MaximizeIcon is null while InitializeComponent is still applying properties.
            if (change.Property == WindowStateProperty && MaximizeIcon is not null)
            {
                MaximizeIcon.Kind = WindowState == WindowState.Maximized
                    ? Material.Icons.MaterialIconKind.WindowRestore
                    : Material.Icons.MaterialIconKind.WindowMaximize;
            }
        }

        /// <summary>
        /// Asks before closing, whichever way the close was requested (caption button, Alt+F4, the macOS
        /// red button). Application shutdown (tray Exit, Cmd+Q, OS log-off) is not asked about.
        /// </summary>
        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            if (_closeConfirmed || e.Cancel ||
                e.CloseReason is WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown)
            {
                return;
            }

            // Cancelled up front because the question is asynchronous; the window is closed again below.
            e.Cancel = true;

            try
            {
                if (await _dialog.Question(LocalizationService.Instance.GetString("Shell_ConfirmExit")))
                {
                    _closeConfirmed = true;

                    // The shell closes on main-window close (App.axaml.cs), which runs the Exit handlers:
                    // ABP shutdown, Hangfire stop, Serilog flush.
                    Close();
                }
            }
            catch (Exception ex)
            {
                // An async void override faulting after the first await escapes the dispatcher handler.
                Log.Error(ex, "Close confirmation failed.");
            }
        }

        private void SetMenuCollapsed(bool collapsing)
        {
            IsMenuCollapsed = collapsing;

            // Width and opacity carry DoubleTransitions in MainWindow.axaml, so setting the target value is
            // the whole animation; a toggle mid-way simply retargets from the current value. The labels
            // fade themselves out off the "menu-collapsed" class (see Themes/Navigation.axaml).
            SideMenu.Classes.Set("menu-collapsed", collapsing);
            SideMenu.Width = collapsing ? CollapsedMenuWidth : ExpandedMenuWidth;
            StackHeader.Opacity = collapsing ? 0d : 1d;
        }

        /// <summary>
        /// Handler for <c>TabCloseItem.CloseClick</c>; wire it up when switching the main region over to
        /// the tabbed <see cref="Themes.Controls.TabControl"/> (see MainWindow.axaml).
        /// </summary>
        private void OnTabCloseClick(object? sender, RoutedEventArgs e)
        {
            if (e.Source is TabCloseItem { Content: UserControl view })
            {
                _navigationService.RemoveView(view);
            }
        }
    }
}
