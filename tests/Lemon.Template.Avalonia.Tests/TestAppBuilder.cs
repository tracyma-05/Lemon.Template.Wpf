using Avalonia;
using Avalonia.Headless;
using Lemon.Template.Avalonia.Tests;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

// Sequential on purpose. Test classes running in parallel race each other while the headless platform
// starts up ("The calling thread cannot access this object"), and several tests change process-wide state
// anyway: the current culture, LocalizationService.Instance, and the Material theme.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Lemon.Template.Avalonia.Tests;

/// <summary>
/// Headless host for <c>[AvaloniaFact]</c> tests.
/// </summary>
/// <remarks>
/// Uses the real <see cref="App"/> so the Material theme and the shell styles from App.axaml are loaded.
/// That is safe here: the headless platform has no desktop lifetime, so App skips the whole ABP start-up.
/// Skia rendering (rather than headless drawing) lets a test capture a frame when it needs one.
/// </remarks>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
