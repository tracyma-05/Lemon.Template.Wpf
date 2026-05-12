namespace Lemon.Template.Wpf.Services.Theming;

/// <summary>Material base theme + optional primary/secondary overrides (ARGB). Null colors mean keep BundledTheme defaults from XAML.</summary>
public sealed record AppThemeSnapshot(bool IsDark, int? PrimaryArgb, int? SecondaryArgb);
