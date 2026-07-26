# Contributing

Thanks for helping out. This repository is both an application and a `dotnet new` template, so a change
usually has to be correct in two places: the code that runs, and the code that gets scaffolded.

## Prerequisites

- A .NET SDK matching the target framework in `src/Lemon.Template.Wpf/Lemon.Template.Wpf.csproj`
  (currently the **.NET 10** SDK).
- **WebView2 Runtime**, if you are touching the Tools → Cron page.

## Build and test

```bash
dotnet build Lemon.Template.Wpf.sln -c Debug
```

```bash
dotnet test
```

Both must be clean before you open a pull request. `TreatWarningsAsErrors` is on, so a warning is a build
failure — that is deliberate.

CI runs the same two commands on `windows-latest`, plus the template round-trip described below. See
[`.github/workflows/ci.yml`](.github/workflows/ci.yml).

## Conventions

- **Package versions** live only in `Directory.Packages.props`. A `PackageReference` with a `Version`
  attribute will fail Central Package Management. Adding a dependency means one line in each file.
- **Released versions only.** No pre-release pins on `main`, and never a CI-feed build (`-ci1234`): those
  vanish when the feed rotates and break restore for everyone who scaffolded from the template. Trial
  pre-releases on a branch and note them in `CHANGELOG.md`.
- **Shared build properties** live in `Directory.Build.props`; package metadata and the shipped version
  live in `common.props`.
- **Formatting** follows `.editorconfig`. Style/IDE analyzer rules are `suggestion` severity on purpose:
  they should guide in the editor, not block a merge. Correctness rules stay as errors.
- **Nullable reference types are enabled** everywhere. The only opt-outs are
  `Infrastructures/Dialogs/ParametersBase.cs` and `ParametersExtensions.cs`, which carry a comment
  explaining why. Do not add new `#nullable disable` without one.
- **Don't call `Environment.Exit`.** Use `Application.Current.Shutdown()` so `App.OnExit` can stop the ABP
  host and flush Serilog.
- **Don't dispose view models on `Unloaded`.** WPF raises it when a view is only temporarily detached. Go
  through `INavigationService.RemoveView` / `ViewModelLocator.ReleaseViewModel`.
- **User-facing strings go in `Resources/AppStrings*.resx`**, not in XAML or C# literals. Add the key to
  every culture file.

## Adding a page

See [Adding a page](README.md#adding-a-page). In short: view + view model by naming convention, a route
constant, a `[NavigationRegister]` attribute, and `Menu_*` resource entries for the label.

## Changing the template itself

Anything under `.template.config/`, a new feature switch, or a new top-level file needs a round-trip check —
building the repository is *not* enough, because the template engine strips conditional blocks that the
repository compiles with enabled.

```bash
dotnet new install .
```

```bash
dotnet new lemon-wpf -n Acme.FullApp -o /tmp/Full
```

```bash
dotnet new lemon-wpf -n Acme.MinApp -o /tmp/Min --EnableHangfire false --EnableTrayIcon false --IncludeTests false
```

Then build **both** outputs, and run the scaffolded tests for the full one. Watch for:

- leftover `#if (Enable...)` / `<!--#if ... -->` markers in the generated files;
- references to a feature you disabled (packages, `using` directives, `FrameworkReference`);
- strings that should have been renamed by `sourceName` but were not — the resource base name in
  `LocalizationService` is one of these, and the localization tests are what catch it.

Finally, `dotnet new uninstall .` so you do not leave a local template registered.

New files at the repository root must also be added to the `<Content Include=...>` list in
`Lemon.Template.Wpf.TemplatePack.csproj`, or they will be missing from the published package.

## Commits and pull requests

- Keep a pull request to one concern; this template is meant to stay small and easy to fork.
- For anything that changes behaviour, say in the description what you ran to verify it.
- Update `CHANGELOG.md` under **Unreleased**.
