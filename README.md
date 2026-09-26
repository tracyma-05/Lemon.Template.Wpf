# Material Design Desktop Starters (WPF · Avalonia)

[![CI](https://github.com/tracyma-05/Lemon.Template.Wpf/actions/workflows/ci.yml/badge.svg)](https://github.com/tracyma-05/Lemon.Template.Wpf/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.txt)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20macOS-0078D4)](https://github.com/)

Two **`dotnet new` templates** for Material Design desktop apps with **dependency injection**, **SQLite-backed settings & jobs**, navigation, localization and file logging:

| Template | UI | Runs on | Short name |
|----------|----|---------|------------|
| WPF | WPF + [Material Design In XAML](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit) | Windows | `lemon-wpf` |
| Avalonia | [Avalonia](https://avaloniaui.net/) + [Material.Avalonia](https://github.com/AvaloniaCommunity/Material.Avalonia) | **Windows and macOS** | `lemon-avalonia` |

Both ship in the same NuGet package (`Lemon.Templates.Wpf`) and share the same architecture; most of this README applies to both, and [Avalonia template (Windows / macOS)](#avalonia-template-windows--macos) lists what differs.

---

## :bookmark_tabs: Table of contents

- [Overview](#overview)
- [Features](#features)
- [Tech stack](#tech-stack)
- [Architecture notes](#architecture-notes)
- [Avalonia template (Windows / macOS)](#avalonia-template-windows--macos)
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

It is also packaged as **.NET project templates** (`.template.config/template.json` for WPF, `templates/avalonia/.template.config/template.json` for Avalonia) so you can install them locally or ship them in a NuGet template package.

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
| **Check for updates** (WPF) | Title-bar button that reads a JSON update manifest and offers the download. Present only when `Update:Enabled` is `true` **and** `Update:Url` is set; can also check quietly on start-up. See [Check for updates](#check-for-updates-wpf). |
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

## :apple: Avalonia template (Windows / macOS)

`src/Lemon.Template.Avalonia` is the same application rebuilt on **Avalonia 12** so a generated project runs
on **Windows and macOS** from one code base (`net10.0`, no `-windows` TFM). Pages, navigation, dialogs,
theming, localization, SQLite stores, ABP/Autofac and Hangfire are ported one-to-one; the view models and
services are largely unchanged.

| Layer | Avalonia template |
|-------|-------------------|
| **UI** | Avalonia 12, [Material.Avalonia](https://github.com/AvaloniaCommunity/Material.Avalonia), [Material.Icons.Avalonia](https://github.com/AvaloniaUtils/Material.Icons.Avalonia), [DialogHost.Avalonia](https://github.com/AvaloniaUtils/DialogHost.Avalonia) |
| **Bindings** | Compiled bindings by default (`x:DataType` on every view) |
| **Tray** | Avalonia's built-in `TrayIcon` — notification area on Windows, menu bar extra on macOS |
| **Tests** | xUnit v3 + `Avalonia.Headless.XUnit` (`[AvaloniaFact]`), runs on Windows and macOS |
| **Target** | `net10.0` |

### What differs from the WPF template

| Area | WPF | Avalonia |
|------|-----|----------|
| **Modal window dialogs** | `IDialogService.ShowDialog(name, params, callback)`, `IHostDialogService.ShowWindow(...)` (synchronous) | `await IDialogService.ShowWindowAsync(name, params)` — an Avalonia modal window cannot block the caller. In-window `DialogHost` dialogs (`ShowDialogAsync`) are unchanged. |
| **Tools → Cron** | Hangfire dashboard embedded with WebView2 | Shows the dashboard URL and an **Open in browser** button: WebView2 is Windows-only, and the system browser works the same on both platforms with no extra native dependency. |
| **Window chrome** | Borderless window with drawn min/max/close buttons | Client area extends into the title bar with `WindowDecorations="BorderOnly"`. Windows gets the template's own min/max/close buttons; macOS keeps its native traffic lights. `WindowDecorationProperties.ElementRole` marks the title strip (`TitleBar`, so the OS handles drag, double-click and snapping), the caption buttons, and every clickable control on the strip (`User`). Closing asks for confirmation whichever way it is requested (caption button, Alt+F4, the red button); tray **Exit**, Cmd+Q and OS shutdown do not ask. |
| **Theme switching** | `PaletteHelper` on MDIX's `BundledTheme` | `CustomMaterialTheme` with `BaseTheme="Inherit"`: light / dark is only `Application.RequestedThemeVariant`, and primary / secondary are the theme's `Color` properties. (`MaterialTheme` rebuilds its palette from an enum asynchronously on every switch, which undid the switch.) On a dark base the mid primary is replaced by its light shade for contrast, as MDIX's `ColorAdjustment` does. |
| **Fonts** | MDIX default | **Bundled** in `Assets/Fonts`, so every Windows and macOS machine renders the same: Noto Sans SC Regular / Medium / Bold (static OTFs from [notofonts/noto-cjk](https://github.com/notofonts/noto-cjk)) as `UiFont`, and Cascadia Mono (from [google/fonts](https://github.com/google/fonts/tree/main/ofl/cascadiamono)) as `CodeFont`, applied to every window. Both are SIL OFL 1.1; the licence texts ship next to the executable in `Licenses/Fonts`. Material.Avalonia's default Roboto has no CJK glyphs, which split mixed Chinese / Latin text across two fonts. Static weights rather than the variable Noto file because Avalonia does not apply a variable font's weight axis (it renders everything at the default Thin). Cost: about 26 MB per app. |
| **Log files** | `<app folder>/Logs` | `<LocalApplicationData>/<AssemblyName>/Logs` (`%LOCALAPPDATA%` on Windows, `~/Library/Application Support` on macOS) — an `.app` bundle is not writable. |
| **Desktop shortcut** | `--EnableDesktopShortcut` | Same option, **Windows only** (`.lnk` via COM); a no-op on macOS. |
| **Localization refresh** | `LocalizationService` raises `"Item[]"` | Raises `"Item"`, the name Avalonia's indexer bindings listen for (covered by `LocalizeExtensionTests`). |
| **Visibility converters** | `BoolToVisibility`, `InverseBoolToVisibility`, `CountToVisibility` | Avalonia has no `Visibility`: bind `IsVisible` directly (`{Binding !Flag}` / `{Binding !!Items.Count}`), or use `CountToBoolConverter`. |

### Run from source

```bash
dotnet run --project src/Lemon.Template.Avalonia/Lemon.Template.Avalonia.csproj
```

```bash
dotnet test tests/Lemon.Template.Avalonia.Tests/Lemon.Template.Avalonia.Tests.csproj
```

On macOS use those two commands rather than building the whole solution: `Lemon.Template.Wpf.sln` also
contains the WPF projects, which only build on Windows. The headless view tests write the frames they render
to `renders/` next to the test assembly, which is a quick way to check a page's look on a machine you are not
sitting at.

### Publishing

```bash
dotnet publish src/Lemon.Template.Avalonia/Lemon.Template.Avalonia.csproj -c Release -r win-x64 --self-contained
```

```bash
dotnet publish src/Lemon.Template.Avalonia/Lemon.Template.Avalonia.csproj -c Release -r osx-arm64 --self-contained
```

Use `osx-x64` for Intel Macs. To get a double-clickable **`.app` bundle**, run this on a Mac:

```bash
bash src/Lemon.Template.Avalonia/Packaging/macOS/bundle.sh
```

It publishes, assembles `<AppName>.app` with `Packaging/macOS/Info.plist`, generates the icon with `sips` /
`iconutil`, and signs the bundle ad hoc so it launches locally. Set `CFBundleIdentifier` in `Info.plist` to
your own reverse-DNS name first. Shipping to other Macs additionally needs a Developer ID signature and
notarization, which the script does not do.

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

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs these jobs:

- **Build and test (Windows)** — restore, build the whole solution in `Release` (WPF and Avalonia), run both
  test suites, upload the `.trx`.
- **Build and test (macOS, Avalonia)** — run the Avalonia test suite on `macos-latest`, publish an
  `osx-arm64` build, and upload the test results plus the frames the headless view tests rendered.
- **Pack** — build the template package once; every round-trip below installs that exact `.nupkg`.
- **Template round-trip** — `lemon-wpf` on Windows, `lemon-avalonia` on Windows *and* macOS: scaffold with
  default options and with every optional feature disabled, build both, run the scaffolded tests, assert the
  disabled features left no files/dependencies/conditional markers behind (and that neither template leaks
  the other's sources). The Avalonia leg also builds a scaffold with the Windows-only desktop shortcut on.

The round-trip exists because building this repository does **not** prove the templates work: the template
engine strips conditional blocks that the repository compiles with enabled.

---

## :package: Using as a `dotnet new` template

Install from a **local clone** of this repository (the folder that contains `.template.config`); this registers both `lemon-wpf` and `lemon-avalonia`:

```bash
dotnet new install .
```

The repository root **`Lemon.Template.Wpf.sln`** is the full developer solution (both apps, both test projects, the template pack and the publisher tool). Each template ships a slimmer solution that only loads its app and a few shared files—see **`packaging/Lemon.Template.Wpf.sln`** and **`packaging/Lemon.Template.Avalonia.sln`** (paths are written for the generated layout, not for opening from `packaging/` on disk).

Create a new project (default `sourceName` is `Lemon.Template.Wpf`; replace `-n` / `-o` with your app name):

```bash
dotnet new lemon-wpf -n MyCompany.MyApp -o MyCompany.MyApp
```

or, for Windows **and** macOS (default `sourceName` `Lemon.Template.Avalonia`):

```bash
dotnet new lemon-avalonia -n MyCompany.MyApp -o MyCompany.MyApp
```

### Options

| Option | Default | Effect |
|--------|---------|--------|
| `--EnableHangfire` | `true` | Hangfire job server and the dashboard page (embedded with WebView2 in WPF, opened in the browser in Avalonia). Off also drops the `Microsoft.AspNetCore.App` framework reference, WebView2 (WPF) and both Hangfire packages, and omits `Services/Hangfire`, `Views/Tools` and `ViewModels/Tools`. |
| `--EnableTrayIcon` | `true` | Tray icon with an Exit command; restores the window on left click. WPF uses `H.NotifyIcon.Wpf`; Avalonia uses its built-in `TrayIcon` (a menu bar extra on macOS, where a click opens the menu). |
| `--EnableDesktopShortcut` | `false` | Recreates a desktop shortcut on every launch. Off by default because silently writing to the user's desktop is surprising for a fresh app. Windows only; the Avalonia app skips it on macOS. |
| `--IncludeTests` | `true` | The xUnit test project under `tests/`. |
| `--skipRestore` | `false` | Skip the implicit `dotnet restore` after creation. |

A minimal shell with no background jobs and no tray icon:

```bash
dotnet new lemon-wpf -n MyCompany.MyApp --EnableHangfire false --EnableTrayIcon false
```

The packaged solutions load the application project only; add the
test project with `dotnet sln add tests/*/*.csproj` if you want it in the same solution.

Uninstall when you no longer need the template:

```bash
dotnet new uninstall <path-to-this-repo>
```

To publish the template on **NuGet**, this repo includes **`Lemon.Template.Wpf.TemplatePack.csproj`** (package id **`Lemon.Templates.Wpf`**) and a small publisher tool under **`tools/NuGet.TemplatePublisher`** that uses **NuGet.Protocol** + **NuGet.Packaging** to `dotnet pack` and push. See [Publish template to NuGet](#publish-template-to-nuget).

---

## :satellite: Publish template to NuGet

Two paths ship side by side: **CI** (recommended — nuget.org's Trusted Publishing, no stored key) and the **local publisher tool** (API key, unchanged).

### From CI (Trusted Publishing)

Push a `vX.Y.Z` tag — the `publish` job in `.github/workflows/ci.yml` runs after build, test and the template round-trip, downloads the `.nupkg` that job packed, and pushes it. Tag `v1.0.2` must match `Version` in `common.props`; the job fails the check otherwise.

```bash
git tag v1.0.2 && git push origin v1.0.2
```

`workflow_dispatch` with **Push the packed template to nuget.org** checked does the same from a branch (no version check).

The job asks nuget.org for a short-lived key over OIDC (`NuGet/login`, needs `id-token: write`). nuget.org binds that policy to **this workflow file** and the **`production`** environment, so renaming `ci.yml` or the environment breaks the exchange — update the policy in *nuget.org → Trusted Publishing* alongside any rename.

Repository settings it reads:

| Setting | Kind | Purpose |
| --- | --- | --- |
| `NUGET_USER` | variable | nuget.org account the policy belongs to. Defaults to `tracy.ma`. |
| `NUGET_USE_TRUSTED_PUBLISHING` | variable | Set to `false` to skip OIDC and use the API key. |
| `NUGET_API_KEY` | secret | Fallback key, used whenever OIDC yields nothing. |

If neither credential is available the job fails rather than skipping the push silently.

### Locally (API key)

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
| `Update:Enabled` | WPF. `false` by default. Turns the check-for-updates feature on. |
| `Update:Url` | WPF. Absolute http(s) URL of the update manifest. The title-bar button appears only when this is set **and** `Update:Enabled` is `true`. |
| `Update:CheckOnStartup` | WPF. `true` by default. Check quietly once the main window is shown; a dialog appears only when a newer version exists. |

Serilog writes rolling files to `Logs/log-YYYYMMDD.txt`, which is also where the Logs → Local-Logs page
reads from: under the **application base directory** (`AppContext.BaseDirectory`) in the WPF app, and under
`<LocalApplicationData>/<AssemblyName>/Logs` in the Avalonia app (see
[What differs](#what-differs-from-the-wpf-template)).

Minimum levels are set in `App.OnStartup` and differ per configuration: **Release** records `Warning` and
above, **Debug** records `Information` and above. The `Microsoft` namespace is capped at `Warning` in both.

User state lives in the shared SQLite database: `app_theme` (base theme + primary/secondary ARGB) and
`app_language` (selected culture name).

### Check for updates (WPF)

```json
"Update": {
  "Enabled": true,
  "Url": "https://example.com/updates/myapp/latest.json",
  "CheckOnStartup": true
}
```

`Update:Url` must return the latest release as JSON (property names are case-insensitive):

```json
{
  "version": "1.2.0",
  "downloadUrl": "https://example.com/downloads/MyApp-1.2.0.zip",
  "releaseNotes": "- Fixed …",
  "publishedAt": "2026-09-26T08:00:00Z"
}
```

- `version` and `downloadUrl` are required. `version` accepts `1.2`, `1.2.3`, `1.2.3.4` and a leading `v`;
  SemVer suffixes (`-beta`, `+sha`) are ignored. It is compared with the app's own version, i.e. the
  `Version` property of the project (inherited from `common.props` in a generated project).
- `downloadUrl` may be relative to the manifest URL. Only http(s) links are accepted, because **Download**
  opens it in the default browser.
- A start-up check never shows an error: offline or unreachable servers are only logged. Clicking the
  button reports "up to date", "new version" or a readable failure. A red dot on the button marks a pending
  update.

Any static file host works for the manifest.

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
| Tools → Cron is blank (WPF) | The **WebView2 Runtime** is missing. Install the [Evergreen runtime](https://developer.microsoft.com/microsoft-edge/webview2/). |
| Tools → Cron says the dashboard is not running (Avalonia) | The loopback Kestrel host failed to start — usually a fixed `HangfireDashboard:Url` whose port is taken. Leave it empty for a dynamic port; the log has the details. |
| macOS: "the app is damaged" / cannot be opened | The bundle was downloaded (quarantined) without a Developer ID signature and notarization. For a local test build, `xattr -dr com.apple.quarantine <App>.app`; to distribute, sign and notarize. |
| `dotnet build` of the solution fails on macOS | Expected: the solution includes the WPF projects. Build or test the Avalonia projects directly (see the Avalonia *Run from source* commands). |
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
├── templates/avalonia/.template.config/   # lemon-avalonia manifest (reads the repo root via "source": "../../")
├── packaging/                 # Consumer .sln files used only inside the templates (not the full dev solution)
├── Lemon.Template.Wpf.TemplatePack.csproj   # NuGet template pack (PackageType=Template), both templates
├── src/Lemon.Template.Wpf/    # Main WPF application
│   ├── Commons/               # Shared constants (routes, regions, icons)
│   ├── Infrastructures/       # DI extensions, navigation, dialogs, localization, data paths, shell
│   ├── Resources/             # AppStrings.resx (+ per-culture satellites)
│   ├── Services/              # Theming, localization store, Hangfire host, cron sample jobs
│   ├── Themes/                # Control templates and shared styles (incl. Navigation.xaml)
│   ├── Views/ / ViewModels/   # UI + MVVM
│   └── appsettings.json
├── src/Lemon.Template.Avalonia/        # The same app on Avalonia (Windows / macOS); same folder structure
│   └── Packaging/macOS/               # Info.plist + bundle.sh for a .app bundle
├── tests/Lemon.Template.Wpf.Tests/   # xUnit suite
├── tests/Lemon.Template.Avalonia.Tests/   # xUnit v3 + Avalonia headless suite
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
