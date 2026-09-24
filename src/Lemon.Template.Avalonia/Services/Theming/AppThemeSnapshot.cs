namespace Lemon.Template.Avalonia.Services.Theming;

/// <summary>Material base theme + optional primary/secondary overrides (ARGB). Null colors mean keep the MaterialTheme defaults from App.axaml.</summary>
public sealed record AppThemeSnapshot(bool IsDark, int? PrimaryArgb, int? SecondaryArgb);
