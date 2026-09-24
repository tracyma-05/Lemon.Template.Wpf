using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Material.Icons;
using Material.Icons.Avalonia;

namespace Lemon.Template.Avalonia.Infrastructures.Exceptions;

/// <summary>
/// Minimal modal message window: Avalonia has no <c>MessageBox</c>, and the failures it reports can
/// happen before the ABP host (and therefore the dialog services) exists.
/// </summary>
internal sealed class ErrorWindow : Window
{
    private ErrorWindow(string message, string caption, bool isWarning)
    {
        Title = caption;
        SizeToContent = SizeToContent.WidthAndHeight;
        MaxWidth = 560;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var okButton = new Button
        {
            Content = "OK",
            IsDefault = true,
            IsCancel = true,
            MinWidth = 88,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        okButton.Click += (_, _) => Close();

        Content = new DockPanel
        {
            Margin = new Thickness(20),
            Children =
            {
                new StackPanel
                {
                    [DockPanel.DockProperty] = Dock.Bottom,
                    Margin = new Thickness(0, 20, 0, 0),
                    Children = { okButton },
                },
                new MaterialIcon
                {
                    [DockPanel.DockProperty] = Dock.Left,
                    Kind = isWarning ? MaterialIconKind.AlertOutline : MaterialIconKind.AlertCircleOutline,
                    Width = 32,
                    Height = 32,
                    Margin = new Thickness(0, 0, 16, 0),
                    VerticalAlignment = VerticalAlignment.Top,
                },
                new SelectableTextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            },
        };
    }

    /// <summary>
    /// Shows the message and completes when the user dismisses it. Modal to the main window when there
    /// is a visible one; otherwise a free-standing window (start-up failure, splash only).
    /// </summary>
    /// <remarks>Must be called on the UI thread.</remarks>
    public static Task ShowAsync(string message, string caption, bool isWarning)
    {
        var window = new ErrorWindow(message, caption, isWarning);

        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow;
        if (owner is { IsVisible: true } && owner is not Views.SplashWindow)
        {
            return window.ShowDialog(owner);
        }

        var closed = new TaskCompletionSource();
        window.Closed += (_, _) => closed.TrySetResult();
        window.Show();
        return closed.Task;
    }
}
