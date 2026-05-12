# Material Design WPF Starter

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.txt)
[![.NET](https://img.shields.io/badge/.NET-net11.0--windows-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
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
| **Settings → Theme** | Light/dark base theme, primary/secondary colors, Material swatches; preferences persisted to **SQLite** (`AppThemePreferencesSqliteStore`). |
| **Logs → Local-Logs** | View tail of **Serilog** rolling file logs under the application `Logs` folder. |
| **Tools → Cron** | Embedded **Hangfire Dashboard** (WebView2) against local storage; sample recurring job (`sample-heartbeat`) for demonstration. |
| **Tray icon** | Optional system tray integration via **H.NotifyIcon.Wpf** (exit from context menu). |
| **Splash** | Lightweight splash on startup. |
| **Navigation** | Pages discovered via `[NavigationRegister("Group/Name", ...)]` and grouped menus built at runtime. |

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
| **Target** | `net11.0-windows` |

---

## :building_construction: Architecture notes

- **`WpfModule`** (ABP module) registers configuration, SQLite `JobStorage`, theme services, Hangfire dashboard host, keyed/navigation services, and wires **ViewModelLocator** on `FrameworkElement.Loaded`.
- **Single SQLite file** (default: `%LocalApplicationData%\<AssemblyName>\<AssemblyName>.db`) holds Hangfire schema and app tables (e.g. theme preferences). Path is overridable via configuration.
- **Regions**: main content uses `RegionManagerAttached` with a central `INavigationService` mapping route names to views.

---

## :door: Getting started

### Prerequisites

- [Windows SDK / .NET SDK](https://dotnet.microsoft.com/download) supporting **.NET 11** and **WPF** (`net11.0-windows`).
- **WebView2 Runtime** (usually present on modern Windows; required for the Hangfire tools page).

### Run from source

```bash
git clone https://github.com/tracyma-05/Lemon.Template.Wpf
cd Lemon.Template.Wpf   # or your fork folder name
dotnet restore
dotnet build src/Lemon.Template.Wpf/Lemon.Template.Wpf.csproj -c Release
dotnet run --project src/Lemon.Template.Wpf/Lemon.Template.Wpf.csproj -c Release
```

Open the solution in Visual Studio / Rider if you prefer an IDE workflow.

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

Serilog writes rolling files to `Logs/log-YYYYMMDD.txt` under the working directory (see `App.xaml.cs`).

---

## :file_folder: Repository layout

```
Lemon.Template.Wpf/
├── .template.config/          # dotnet new template manifest
├── .templateignore            # Files excluded when packing/installing the template
├── packaging/                 # Consumer .sln used only inside the NuGet template (not the full dev solution)
├── Lemon.Template.Wpf.TemplatePack.csproj   # NuGet template pack (PackageType=Template)
├── src/Lemon.Template.Wpf/    # Main WPF application
│   ├── Commons/               # Shared constants
│   ├── Infrastructures/       # DI extensions, navigation, dialogs, data paths
│   ├── Services/              # Theming, Hangfire host, cron sample jobs
│   ├── Views/ / ViewModels/   # UI + MVVM
│   └── appsettings.json
├── tools/
│   └── NuGet.TemplatePublisher/   # Pack + push to NuGet (NuGet.Protocol / NuGet.Packaging)
└── LICENSE.txt
```

---

## :handshake: Contributing

Issues and pull requests are welcome. For larger changes, please open an issue first to discuss direction (keeps the template intentionally small and easy to fork).

---

## :page_with_curl: License

This project is licensed under the **MIT License** — see [LICENSE.txt](LICENSE.txt).
