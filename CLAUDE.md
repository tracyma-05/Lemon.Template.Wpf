# Lemon.Template.Wpf

Source of the `lemon-wpf` and `lemon-avalonia` `dotnet new` templates (`src/Lemon.Template.Wpf`,
`src/Lemon.Template.Avalonia`). Conditional blocks (`#if (EnableTrayIcon)` etc.) are template symbols; building
the repository is not enough after touching them, see the round-trip check in `CONTRIBUTING.md`. Apps generated
from this template (e.g. Lemon.Hub.Wpf) pick up changes by hand, so note ported features in their changelogs.

## Localization is required for every new menu entry, page or dialog

Both apps ship in English and Chinese (`zh-CN`), and the language can be switched at run time in
Settings → Language without a restart. Nothing user-visible may be a hard-coded literal.

- **Strings live in both** `Resources/AppStrings.resx` (English) **and** `AppStrings.zh-CN.resx` (Chinese) of
  the app you change. Add every key to both files in the same change. When a feature exists in both apps, add it
  to both apps' resx files. Name keys `<Area>_<What>`, e.g. `Theme_Title`, `Update_Error_Network`.
- **Menu entries**: a route such as `Logs/Local-Logs` in `Commons/Constants.cs` needs a `Menu_<segment>` key per
  segment, with every non-alphanumeric character turned into `_` (`Menu_Logs`, `Menu_Local_Logs`). Without it
  the menu shows the raw route name.
- **XAML**: `Text`, `Content`, `ToolTip`, `HintAssist.Hint` / `HelperText` and similar use `{loc:Localize Key}`.
  Design-time `d:Text` may stay a literal.
- **Text mixed with bound values (WPF)**: do not use `StringFormat` or `TargetNullValue` with literal text. They
  are fixed when the XAML loads and ignore a language switch. Use a `MultiBinding` with
  `{converters:FormatConverter}` (first child `<loc:LocalizeExtension Key="…" />` is the format string, the rest
  fill `{0}`, `{1}`, …), or `{converters:FirstNonEmptyConverter}` for "value, else a localized placeholder".
- **C#** (view models, services, dialog titles, snackbar messages, exception messages shown to the user):
  `LocalizationService.Instance.GetString("Key")` / `.Format("Key", args)`. Text a view model builds and keeps on
  screen must be raised again on `LocalizationService.Instance.PropertyChanged`.
- Log messages (`Log.*`, `ILogger`) stay English and are not localized.
- `LocalizationCoverageTests` (WPF tests) fails when the two resx files have different keys, or when source code
  uses a key that neither file defines. Run `dotnet test Lemon.Template.Wpf.sln` before committing.
