using System.IO;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Lemon.Template.Avalonia.Infrastructures.Dialogs;
using Lemon.Template.Avalonia.Infrastructures.Localization;
using Lemon.Template.Avalonia.Services.Localization;
using Lemon.Template.Avalonia.Services.Theming;
using Lemon.Template.Avalonia.Services.Updates;
using Lemon.Template.Avalonia.ViewModels.Dialogs;
using Lemon.Template.Avalonia.ViewModels.Settings;
using Lemon.Template.Avalonia.Views.Dialogs;
using Lemon.Template.Avalonia.Views.Settings;
using Xunit;

namespace Lemon.Template.Avalonia.Tests;

/// <summary>
/// Smoke tests: each page loads its compiled XAML against the Material theme and renders a frame. A wrong
/// resource key, style class or template fails here instead of at the first click in the running app.
/// </summary>
/// <remarks>
/// Frames are written to <c>renders/</c> (JPEG) next to the test assembly, which is handy for eyeballing a
/// macOS CI run without a Mac.
/// </remarks>
public class ViewRenderingTests
{
    [AvaloniaFact]
    public void ThemeAppearanceView_Renders()
    {
        var viewModel = new ThemeAppearanceViewModel(new AppThemeService(new NullThemeStore()));
        var view = new ThemeAppearanceView { DataContext = viewModel };

        // Pulled from the live Material theme when the page is navigated to.
        viewModel.OnNavigatedTo(null!);
        RenderAndSave(view, "theme");

        Assert.Equal(MaterialDesignSwatches.NameForPrimary(MaterialDesignSwatches.DefaultPrimary), viewModel.SelectedPrimarySwatch);
    }

    [AvaloniaFact]
    public void LanguageView_Renders()
    {
        var viewModel = new LanguageViewModel(LocalizationService.Instance, new NullLanguageStore());
        var view = new LanguageView { DataContext = viewModel };

        RenderAndSave(view, "language");

        Assert.NotEmpty(viewModel.Languages);
    }

    [AvaloniaFact]
    public void UpdateDialogView_Renders()
    {
        var latest = new UpdateManifest(new Version(1, 3, 0, 0), null, "- macOS builds\n- Faster start-up", DateTimeOffset.UtcNow);
        var result = new UpdateCheckResult(
            UpdateCheckStatus.UpdateAvailable, new Version(1, 2, 1, 0), latest, null, new Uri("https://example.com/MyApp-1.3.0-osx-arm64.zip"));

        // The host dialog service is only used to close the dialog, which this test never does.
        var viewModel = new UpdateDialogViewModel(null!, LocalizationService.Instance);
        viewModel.OnDialogOpened(new DialogParameters { { UpdateDialogViewModel.ResultParameter, result } });
        var view = new UpdateDialogView { DataContext = viewModel };

        RenderAndSave(view, "update-dialog");

        Assert.True(viewModel.IsUpdateAvailable);
        Assert.Equal("1.3.0", viewModel.LatestVersion);
    }

    private static void RenderAndSave(Control view, string name)
    {
        var window = new Window { Width = 1000, Height = 760, Content = view };
        window.Show();

        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        var directory = Path.Combine(AppContext.BaseDirectory, "renders");
        Directory.CreateDirectory(directory);
        frame!.Save(Path.Combine(directory, $"{name}.jpg"), new JpegBitmapEncoderOptions { Quality = 90 });

        window.Close();
    }

    private sealed class NullThemeStore : IAppThemePreferencesStore
    {
        public AppThemeSnapshot? Load() => null;

        public void Save(AppThemeSnapshot snapshot)
        {
        }
    }

    private sealed class NullLanguageStore : IAppLanguagePreferencesStore
    {
        public string? Load() => null;

        public void Save(string cultureName)
        {
        }
    }
}
