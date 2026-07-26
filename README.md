# Material Design WPF Starter

[![CI](https://github.com/tracyma-05/Lemon.Template.Wpf/actions/workflows/ci.yml/badge.svg)](https://github.com/tracyma-05/Lemon.Template.Wpf/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.txt)
[![.NET](https://img.shields.io/badge/.NET-net10.0--windows-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D4?logo=windows)](https://github.com/)

A **Windows desktop WPF** starter that combines **Material Design In XAML**, **dependency injection**, **SQLite-backed settings & jobs**, and an optional **`dotnet new` template** for scaffolding similar apps quickly.

---

## :bookmark_tabs: Table of contents

- [Overview](#overview)
- [Features](#features)
- [Tech stack](#tech-stack)
- [Architecture notes](#architecture-notes)
- [Getting started](#getting-started)
- [Using as a `dotnet new` template](#using-as-a-dotnet-new-template)
- [Publish template to NuGet](#publish-template-to-nuget)
- [Configuration](#configuration)
- [Troubleshooting](#troubleshooting)
- [Repository layout](#repository-layout)
- [Contributing](#contributing)
- [License](#license)

---

## :rocket: Overview

This repository is intended as a **clean, generic baseline** for line-of-business style WPF applications: a single main window with a side navigation shell, region-based content, theme persistence, local file logging, and a **Hangfire** dashboard hosted in-process (Kestrel on loopback) with **SQLite** storage shared with other app data.

It is also packaged as a **.NET project template** (see `.template.config/template.json`) so you can install it locally or ship it in a NuGet template package.

---

## :sparkles: Features

| Area | Description |
|------|-------------|
| **Home** | Landing page shown on first launch: application name and version, quick-start tiles that jump to each built-in page, technology summary and external links. Registered as a top-level menu entry with no children. |
| **Settings → Theme** | Light/dark base theme, primary/secondary colors, Material swatches; preferences persisted to **SQLite** (`AppThemePreferencesSqliteStore`). |
| **Settings → Language** | Runtime language switching (English / 简体中文) with no restart; choice persisted to SQLite (`app_language`). |
| **Logs → Local-Logs** | View tail of **Serilog** rolling file logs under the application `Logs` folder, with a status line reporting path, size and truncation. |
| **Tools → Cron** | Embedded **Hangfire Dashboard** (WebView2) against local storage; sample recurring job (`sample-heartbeat`) for demonstration. Optional — see template symbols. |
| **Tray icon** | Optional system tray integration via **H.NotifyIcon.Wpf** (exit from context menu). |
| **Splash** | Lightweight splash on startup. |
| **Navigation** | Pages discovered via `[NavigationRegister("Group/Name", ...)]` and grouped menus built at runtime; a single-segment key (`"Home"`) registers a top-level page with no children. Menu labels resolved from resources. |
| **Localization** | `.resx`-backed strings, `{loc:Localize Key}` markup extension, live culture switching. |
| **Tests** | xUnit suite over navigation registration, localization, theme packing and path resolution. |

---

## :hammer_and_wrench: Tech stack

| Layer | Libraries / runtime |
|-------|---------------------|
| **UI** | WPF, [MaterialDesignThemes](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit) |
| **MVVM** | [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (source generators, `RelayCommand`, etc.) |
| **Composition** | [Volo.Abp](https://github.com/abpframework/abp) (`AbpAutofacModule`, `AbpBackgroundJobsHangfireModule`), Autofac-backed DI |
| **Background jobs** | [Hangfire](https://www.hangfire.io/) + [Hangfire.Storage.SQLite](https://github.com/frankhommers/Hangfire.Storage.SQLite), embedded ASP.NET Core host for dashboard |
| **WebView** | [Microsoft.Web.WebView2](https://developer.microsoft.com/microsoft-edge/webview2/) (Hangfire UI inside WPF) |
| **Data** | [Microsoft.Data.Sqlite](https://learn.microsoft.com/dotnet/standard/data/sqlite/) |
| **Logging** | [Serilog](https://serilog.net/) (async file sink, rolling daily) |
| **Localization** | `System.Resources` satellite assemblies (`Resources/AppStrings*.resx`) |
| **Tests** | [xUnit](https://xunit.net/) |
| **Build** | Central Package Management (`Directory.Packages.props`), shared conventions (`Directory.Build.props`), `.editorconfig`, warnings-as-errors |
| **Target** | `net10.0-windows` |

> **Dependency policy.** Every package is pinned to a **released** version on nuget.org — no pre-release,
> and in particular no CI-feed builds, which stop resolving once the producing feed rotates them.
> The toolchain is released too: `net10.0-windows` builds on the GA **.NET 10 SDK**.

---

## :building_construction: Architecture notes

- **`WpfModule`** (ABP module) registers configuration, SQLite `JobStorage`, theme and localization services, the Hangfire dashboard host, keyed/navigation services, and wires **ViewModelLocator** on `FrameworkElement.Loaded`.
- **Single SQLite file** (default: `%LocalApplicationData%\<AssemblyName>\<AssemblyName>.db`) holds the Hangfire schema and app tables (`app_theme`, `app_language`). Path is overridable via configuration.
- **Regions**: main content uses `RegionManagerAttached` with a central `INavigationService` mapping route names to views.
- **ViewModel lifetime**: `ViewModelLocator.AutoWireViewModel` creates a DI scope per view; `ViewModelLocator.ReleaseViewModel` disposes the ViewModel and its scope. Release is driven by the navigation layer (`INavigationService.RemoveView`, and content replacement in a `ContentControl` region) rather than by `Unloaded`, which WPF also raises when a view is only temporarily detached.
- **Shutdown**: closing the window or choosing tray → Exit calls `Application.Shutdown()`, so `App.OnExit` runs the ABP shutdown (stopping the Hangfire server and dashboard) before `Log.CloseAndFlush()`. Avoid `Environment.Exit`, which skips all of it and loses buffered log entries.
- **Localization**: `LocalizationService` resolves `Resources/AppStrings*.resx` for the active culture and raises `Binding.IndexerName` on change, which is what lets `{loc:Localize Key}` bindings refresh without a restart. Menu labels come from `Menu_<RouteName>` keys, so routing keys stay culture-independent.

---

## :door: Getting started

### Prerequisites

- [Windows SDK / .NET SDK](https://dotnet.microsoft.com/download) supporting **.NET 10** and **WPF** (`net10.0-windows`).
- **WebView2 Runtime** (usually present on modern Windows; required for the Hangfire tools page).

### Run from source

```bash
git clone https://github.com/tracyma-05/Lemon.Template.Wpf
cd Lemon.Template.Wpf   # or your fork folder name
dotnet restore
dotnet build src/Lemon.Template.Wpf/Lemon.Template.Wpf.csproj -c Release
dotnet run --project src/Lemon.Template.Wpf/Lemon.Template.Wpf.csproj -c Release
```

Run the tests:

```bash
dotnet test
```

Open the solution in Visual Studio / Rider if you prefer an IDE workflow.

### Build conventions

`Directory.Build.props` and `Directory.Packages.props` apply to every project in the repository:

- **Package versions are centralized.** `PackageReference` entries carry no `Version`; add or bump a
  dependency in `Directory.Packages.props` only.
- **Warnings are errors** (`TreatWarningsAsErrors`), with .NET analyzers on. Style/IDE rules are kept at
  `suggestion` in `.editorconfig` so formatting preferences never block a build.
- **NuGet diagnostics stay warnings** (`NU1507`, `NU1901`-`NU1904`): a new advisory, or a machine with more
  than one configured feed, should not turn a green build red without a source change.
- Two files opt out of nullable analysis with `#nullable disable` and a comment explaining why:
  `Infrastructures/Dialogs/ParametersBase.cs` and `ParametersExtensions.cs`. They are ports of Prism's
  parameter bag, whose contract deliberately returns `default` for a missing key.

### Continuous integration

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs on `windows-latest` in two jobs:

- **Build and test** — restore, build the solution in `Release`, run the test suite, upload the `.trx`.
- **Template round-trip** — install the template from source, scaffold it twice (default options, and with
  every optional feature disabled), build both outputs, run the scaffolded tests, assert the disabled
  features left no files/dependencies/conditional markers behind, then pack the template package.

The second job exists because building this repository does **not** prove the template works: the template
engine strips conditional blocks that the repository compiles with enabled.

---

## :package: Using as a `dotnet new` template

Install from a **local clone** of this repository (folder that contains `.template.config`):

```bash
dotnet new install .
```

The repository root **`Lemon.Template.Wpf.sln`** is the full developer solution (includes the template pack, publisher tool, and nested solution folders). The **NuGet package** ships a slimmer solution that only loads the WPF app and a few shared files—see **`packaging/Lemon.Template.Wpf.sln`** (paths are written for that packaged layout, not for opening from `packaging/` on disk).

Create a new project (default `sourceName` is `Lemon.Template.Wpf`; replace `-n` / `-o` with your app name):

```bash
dotnet new lemon-wpf -n MyCompany.MyApp -o MyCompany.MyApp
```

### Options

| Option | Default | Effect |
|--------|---------|--------|
| `--EnableHangfire` | `true` | Hangfire job server and the embedded dashboard page. Off also drops the `Microsoft.AspNetCore.App` framework reference, WebView2 and both Hangfire packages, and omits `Services/Hangfire`, `Views/Tools` and `ViewModels/Tools`. |
| `--EnableTrayIcon` | `true` | Tray icon with an Exit command; restores the window on left click. Off drops `H.NotifyIcon.Wpf`. |
| `--EnableDesktopShortcut` | `false` | Recreates a desktop shortcut on every launch. Off by default because silently writing to the user's desktop is surprising for a fresh app. |
| `--IncludeTests` | `true` | The xUnit test project under `tests/`. |
| `--skipRestore` | `false` | Skip the implicit `dotnet restore` after creation. |

A minimal shell with no background jobs and no tray icon:

```bash
dotnet new lemon-wpf -n MyCompany.MyApp --EnableHangfire false --EnableTrayIcon false
```

The packaged solution (`packaging/Lemon.Template.Wpf.sln`) loads the application project only; add the
test project with `dotnet sln add tests/*/*.csproj` if you want it in the same solution.

Uninstall when you no longer need the template:

```bash
dotnet new uninstall <path-to-this-repo>
```

To publish the template on **NuGet**, this repo includes **`Lemon.Template.Wpf.TemplatePack.csproj`** (package id **`Lemon.Templates.Wpf`**) and a small publisher tool under **`tools/NuGet.TemplatePublisher`** that uses **NuGet.Protocol** + **NuGet.Packaging** to `dotnet pack` and push. See [Publish template to NuGet](#publish-template-to-nuget).

---

## :satellite: Publish template to NuGet

1. Copy `tools/NuGet.TemplatePublisher/secret.json.example` to `tools/NuGet.TemplatePublisher/secret.json` and set `NuGetPublish:ApiKey` (this path is gitignored).
2. Bump **`Version`** in `common.props` when you ship a new template (the template pack’s **`PackageVersion`** follows `$(Version)`). Optionally override **`NuGetPublish:PackageVersion`** in `appsettings.json` or via env (`NUGET_PUBLISH__*`) for a one-off without editing `common.props`.
3. From the repository root:

```bash
dotnet run --project tools/NuGet.TemplatePublisher/NuGet.TemplatePublisher.csproj -c Release
```

The tool resolves the repo root, resolves the template pack csproj (configured path, default `Lemon.Template.Wpf.TemplatePack.csproj` at the root, or a **single** `*TemplatePack*.csproj` there), runs **`dotnet build`** on **`Lemon.Template.Wpf.sln`** so the latest sources compile (set **`SkipPreBuild`** to `true` to skip), then **`dotnet pack`**, verifies the `.nupkg` with **`PackageArchiveReader`**, and pushes via **`PackageUpdateResource`**.

After indexing on nuget.org, install with:

```bash
dotnet new install Lemon.Templates.Wpf
```

To pin a specific package version, use **`@`** (not `::`, which is deprecated):

```bash
dotnet new install Lemon.Templates.Wpf@1.0.1
```

---

## :gear: Configuration

`appsettings.json` (copied to output directory):

| Key | Purpose |
|-----|---------|
| `App:SqliteDatabasePath` | Optional. Absolute path, or path relative to the app base directory. If empty, the database is created under `%LocalApplicationData%\<AssemblyName>\`. |
| `HangfireDashboard:Url` | Optional. Base URL for the embedded Kestrel host (e.g. `http://127.0.0.1:5088`). If empty, **`http://127.0.0.1:0`** is used (dynamic port). |

Serilog writes rolling files to `Logs/log-YYYYMMDD.txt` under the **application base directory**
(`AppContext.BaseDirectory`), which is also where the Logs → Local-Logs page reads from.

Minimum levels are set in `App.OnStartup` and differ per configuration: **Release** records `Warning` and
above, **Debug** records `Information` and above. The `Microsoft` namespace is capped at `Warning` in both.

User state lives in the shared SQLite database: `app_theme` (base theme + primary/secondary ARGB) and
`app_language` (selected culture name).

### Adding a page

1. Add a `UserControl` under `Views/`, and a matching `<Name>ViewModel` under `ViewModels/` — the naming
   convention in `ViewModelLocator` wires them up automatically.
2. Declare a route constant in `Commons/Constants.cs` (`"Group/Name"` plus a `"GroupIcon/PageIcon"` pair).
   For a page that should sit at the top level with no children, use a single-segment key and a single icon
   (see `Constants.Home`).
3. Annotate the view with `[NavigationRegister(Constants.MyPage, Constants.MainRegion, typeof(UserControl), Constants.MyPageIcon, DisplayOrder = 30)]`.
4. Add `Menu_<Group>` / `Menu_<Name>` entries to every `Resources/AppStrings*.resx` (non-alphanumeric
   characters in the route name become `_`). Without them the menu falls back to the raw route name.

To navigate from code — and keep the side-menu highlight in sync — inject `IMenuNavigator` and call
`NavigateTo(Constants.MyPage)`; `INavigationService` on its own only swaps the region content. The start-up
page is the `NavigateTo` call in `MainWindowViewModel`'s constructor.

### Adding a localized string

Add the key to `Resources/AppStrings.resx` **and** every `AppStrings.<culture>.resx`, then reference it as
`{loc:Localize My_Key}` in XAML, or `ILocalizationService.GetString` / `Format` in a view model. A key that
is missing from the neutral file renders as `[My_Key]` so the gap is visible rather than blank.

---

## :lifebuoy: Troubleshooting

| Symptom | Cause / fix |
|---------|-------------|
| Tools → Cron is blank | The **WebView2 Runtime** is missing. Install the [Evergreen runtime](https://developer.microsoft.com/microsoft-edge/webview2/). |
| `NETSDK1057` / SDK not found | `net10.0-windows` needs the **.NET 10 SDK**. Install it from [dotnet.microsoft.com](https://dotnet.microsoft.com/download). |
| `NU1507` warning about package sources | Central Package Management wants a single feed, or [package source mapping](https://aka.ms/nuget-package-source-mapping) in your `NuGet.config`. Deliberately left as a warning so the template still restores on machines with an internal mirror. |
| A theme or language change is not remembered | The preferences tables live in the shared SQLite file; check `App:SqliteDatabasePath` and that the folder is writable. Failures are logged as warnings rather than thrown. |
| `Recurring job '<name>' can't be scheduled` at startup | A recurring job persisted by an earlier build references a type that no longer exists. Startup now removes such entries automatically and logs a warning; if it recurs, the job type exists but fails to deserialize its arguments. |

---

## :file_folder: Repository layout

```
Lemon.Template.Wpf/
├── .template.config/          # dotnet new template manifest (symbols, excludes, post actions)
├── .templateignore            # Files excluded when packing/installing the template
├── .editorconfig              # Formatting + analyzer severity policy
├── Directory.Build.props      # Shared build conventions (imports common.props)
├── Directory.Packages.props   # Central Package Management: every dependency version
├── common.props               # Package metadata + shipped Version
├── packaging/                 # Consumer .sln used only inside the NuGet template (not the full dev solution)
├── Lemon.Template.Wpf.TemplatePack.csproj   # NuGet template pack (PackageType=Template)
├── src/Lemon.Template.Wpf/    # Main WPF application
│   ├── Commons/               # Shared constants (routes, regions, icons)
│   ├── Infrastructures/       # DI extensions, navigation, dialogs, localization, data paths, shell
│   ├── Resources/             # AppStrings.resx (+ per-culture satellites)
│   ├── Services/              # Theming, localization store, Hangfire host, cron sample jobs
│   ├── Themes/                # Control templates and shared styles (incl. Navigation.xaml)
│   ├── Views/ / ViewModels/   # UI + MVVM
│   └── appsettings.json
├── tests/Lemon.Template.Wpf.Tests/   # xUnit suite
├── tools/
│   └── NuGet.TemplatePublisher/   # Pack + push to NuGet (NuGet.Protocol / NuGet.Packaging)
├── CHANGELOG.md
├── CONTRIBUTING.md
└── LICENSE.txt
```

---

## :handshake: Contributing

Issues and pull requests are welcome. For larger changes, please open an issue first to discuss direction (keeps the template intentionally small and easy to fork).

See [CONTRIBUTING.md](CONTRIBUTING.md) for build/test commands, conventions, and how to verify a change to
the template itself. Notable changes are recorded in [CHANGELOG.md](CHANGELOG.md).

---

## :page_with_curl: License

This project is licensed under the **MIT License** — see [LICENSE.txt](LICENSE.txt).
