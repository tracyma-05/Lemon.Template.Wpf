using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Template.Wpf.Infrastructures.Navigations;
using Lemon.Template.Wpf.Services.Theming;
using MaterialDesignColors;
using System.Windows.Media;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Wpf.ViewModels.Settings;

[ObservableObject]
public partial class ThemeAppearanceViewModel : ISingletonDependency, INavigationAware
{
    private readonly IAppThemeService _appThemeService;
    private bool _suppressSwatchHandlers;
    private bool _muteIsDarkCallback;

    public ThemeAppearanceViewModel(IAppThemeService appThemeService)
    {
        _appThemeService = appThemeService;
        _appThemeService.DarkThemeChanged += OnAppDarkThemeChanged;
    }

    public IReadOnlyList<string> PrimarySwatches => MaterialDesignSwatches.PrimarySwatchNames;

    public IReadOnlyList<string> SecondarySwatches => MaterialDesignSwatches.SecondarySwatchNames;

    [ObservableProperty]
    private bool _isDark;

    [ObservableProperty]
    private int _primaryR;

    [ObservableProperty]
    private int _primaryG;

    [ObservableProperty]
    private int _primaryB;

    [ObservableProperty]
    private int _secondaryR;

    [ObservableProperty]
    private int _secondaryG;

    [ObservableProperty]
    private int _secondaryB;

    [ObservableProperty]
    private string _selectedPrimarySwatch = PrimaryColor.DeepPurple.ToString();

    [ObservableProperty]
    private string _selectedSecondarySwatch = SecondaryColor.Lime.ToString();

    [ObservableProperty]
    private SolidColorBrush _primaryPreview = Brushes.Transparent;

    [ObservableProperty]
    private SolidColorBrush _secondaryPreview = Brushes.Transparent;

    public void OnNavigatedTo(NavigationContext navigationContext) => PullFromActiveTheme();

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
    }

    partial void OnIsDarkChanged(bool value)
    {
        if (_muteIsDarkCallback)
        {
            return;
        }

        _appThemeService.SetDarkTheme(value);
    }

    private void OnAppDarkThemeChanged(object? sender, bool isDark)
    {
        if (IsDark == isDark)
        {
            return;
        }

        _muteIsDarkCallback = true;
        try
        {
            IsDark = isDark;
        }
        finally
        {
            _muteIsDarkCallback = false;
        }
    }

    partial void OnPrimaryRChanged(int value) => RefreshPrimaryPreview();

    partial void OnPrimaryGChanged(int value) => RefreshPrimaryPreview();

    partial void OnPrimaryBChanged(int value) => RefreshPrimaryPreview();

    partial void OnSecondaryRChanged(int value) => RefreshSecondaryPreview();

    partial void OnSecondaryGChanged(int value) => RefreshSecondaryPreview();

    partial void OnSecondaryBChanged(int value) => RefreshSecondaryPreview();

    partial void OnSelectedPrimarySwatchChanged(string value)
    {
        if (_suppressSwatchHandlers || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        try
        {
            var c = MaterialDesignSwatches.ColorFromPrimaryName(value);
            PrimaryR = c.R;
            PrimaryG = c.G;
            PrimaryB = c.B;
        }
        catch (ArgumentException)
        {
            // ignore invalid selection
        }
    }

    partial void OnSelectedSecondarySwatchChanged(string value)
    {
        if (_suppressSwatchHandlers || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        try
        {
            var c = MaterialDesignSwatches.ColorFromSecondaryName(value);
            SecondaryR = c.R;
            SecondaryG = c.G;
            SecondaryB = c.B;
        }
        catch (ArgumentException)
        {
        }
    }

    [RelayCommand]
    private void ApplyTheme()
    {
        var primary = Color.FromRgb((byte)ClampByte(PrimaryR), (byte)ClampByte(PrimaryG), (byte)ClampByte(PrimaryB));
        var secondary = Color.FromRgb((byte)ClampByte(SecondaryR), (byte)ClampByte(SecondaryG), (byte)ClampByte(SecondaryB));
        _appThemeService.ApplyAndPersistTheme(primary, secondary, IsDark);
    }

    [RelayCommand]
    private void ResetDefaultSwatches()
    {
        _appThemeService.ResetDefaultSwatchesPersist();
        PullFromActiveTheme();
    }

    [RelayCommand]
    private void ReloadFromUi()
    {
        PullFromActiveTheme();
    }

    private void PullFromActiveTheme()
    {
        _suppressSwatchHandlers = true;
        _muteIsDarkCallback = true;
        try
        {
            IsDark = _appThemeService.IsDarkTheme();
            var p = _appThemeService.GetPrimaryColor();
            var s = _appThemeService.GetSecondaryColor();
            PrimaryR = p.R;
            PrimaryG = p.G;
            PrimaryB = p.B;
            SecondaryR = s.R;
            SecondaryG = s.G;
            SecondaryB = s.B;
            SelectedPrimarySwatch = MaterialDesignSwatches.NameForPrimary(p);
            SelectedSecondarySwatch = MaterialDesignSwatches.NameForSecondary(s);
            RefreshPrimaryPreview();
            RefreshSecondaryPreview();
        }
        finally
        {
            _suppressSwatchHandlers = false;
            _muteIsDarkCallback = false;
        }
    }

    private void RefreshPrimaryPreview()
    {
        PrimaryPreview = new SolidColorBrush(Color.FromRgb(
            (byte)ClampByte(PrimaryR),
            (byte)ClampByte(PrimaryG),
            (byte)ClampByte(PrimaryB)));
        PrimaryPreview.Freeze();
    }

    private void RefreshSecondaryPreview()
    {
        SecondaryPreview = new SolidColorBrush(Color.FromRgb(
            (byte)ClampByte(SecondaryR),
            (byte)ClampByte(SecondaryG),
            (byte)ClampByte(SecondaryB)));
        SecondaryPreview.Freeze();
    }

    private static int ClampByte(int v) => Math.Clamp(v, 0, 255);
}
