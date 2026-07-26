# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the shipped template package version is the
`Version` property in `common.props`.

## [Unreleased]

### Added

- **Home page.** A landing page (`Views/Home`) shown on first launch, with the application name and version,
  quick-start tiles that navigate to each built-in page, a technology summary and external links. Opening
  a link is restricted to `http`/`https`.
- **Top-level pages with no children.** `[NavigationRegister]` now accepts a single-segment key (`"Home"`)
  alongside `"Group/Name"`; such a page becomes its own menu entry and is rendered with
  `NavigationChildlessItemTemplate` instead of an expander.
- **`IMenuNavigator`.** Navigates by registration key *and* moves the side-menu selection, which
  `INavigationService` alone does not do. Used for the start-up page and the home page's shortcuts, replacing
  the private default-page helper in `MainWindowViewModel`.
- **Localization.** `.resx`-backed strings (`Resources/AppStrings.resx` plus a `zh-CN` satellite), an
  `ILocalizationService`, a `{loc:Localize Key}` markup extension, and a **Settings → Language** page that
  switches the interface language at runtime with no restart. The choice is persisted to SQLite
  (`app_language`). Menu labels resolve from `Menu_<RouteName>` keys, keeping routing keys
  culture-independent.
- **Unit tests.** New `tests/Lemon.Template.Wpf.Tests` xUnit project (50 tests) covering the
  View → ViewModel naming convention, attribute-driven navigation registration (grouping, `DisplayOrder`,
  icon splitting), `NavigationService` route/region validation, `AppSqlitePaths` configuration precedence,
  `ThemeColorArgb` round-tripping, localization resolution and fallbacks, and language persistence.
- **Template options** (`dotnet new lemon-wpf`): `--EnableHangfire`, `--EnableTrayIcon`,
  `--EnableDesktopShortcut`, `--IncludeTests`, `--skipRestore`, plus a restore post-action and a template
  description.
- `INavigationService.RemoveView`, to detach a view from its region and release its ViewModel scope.
- A status line on **Logs → Local-Logs** reporting the resolved path, file size and truncation.
- `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`, `CHANGELOG.md`, `CONTRIBUTING.md`.
- **CI** (`.github/workflows/ci.yml`, `windows-latest`): a build/test job, plus a template round-trip job
  that installs the template from source, scaffolds it with default *and* all-features-disabled options,
  builds both, runs the scaffolded tests, asserts the disabled features left no files, dependencies or
  conditional markers behind, and packs the template package.
- **Publishing from CI.** A `publish` job pushes the packed template to nuget.org on a `vX.Y.Z` tag (or a
  manual run with the `publish` input), after the build and round-trip jobs pass and the tag is checked
  against `Version` in `common.props`. It prefers nuget.org **Trusted Publishing** — a short-lived key
  obtained over OIDC, bound to `ci.yml` and the `production` environment, so no key is stored — and falls
  back to the `NUGET_API_KEY` secret when the exchange yields nothing or `NUGET_USE_TRUSTED_PUBLISHING` is
  `false`. The local `tools/NuGet.TemplatePublisher` API-key flow is unchanged.
- Startup now removes recurring jobs whose job type can no longer be loaded. Renaming or deleting a job
  class used to leave an orphan in SQLite that Hangfire logged as an error on every launch before disabling
  it. Jobs added at runtime through the dashboard are left alone.

### Fixed

- **Exit skipped application shutdown.** The close button and the tray Exit command called
  `Environment.Exit(0)`, bypassing `App.OnExit` — the ABP host was never shut down, the Hangfire server was
  not stopped gracefully, and buffered Serilog entries were lost. Both now call `Application.Shutdown()`,
  and `OnExit` waits (off the dispatcher, with a timeout) for the ABP shutdown before flushing logs.
- **ViewModels were never disposed.** `ViewModelLocator` checked the *view* for `IDisposable` instead of the
  ViewModel, so `IDisposable` ViewModels leaked. It also released the DI scope on the first `Unloaded`,
  which WPF raises whenever a view is temporarily detached, tearing down scoped dependencies of a view that
  was still in use. Release is now explicit and driven by the navigation layer.
- **Startup blocked the UI thread.** Hangfire schema creation and the dashboard host were started with
  `GetAwaiter().GetResult()` inside `OnApplicationInitialization`; these now run through
  `OnApplicationInitializationAsync` and off the dispatcher.
- **Unhandled exceptions could crash the handler.** `MessageBox.Show` was called from
  `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException`, which run on arbitrary
  threads. Dialogs are now marshalled to the dispatcher and de-duplicated; unobserved task exceptions are
  logged and marked observed instead of interrupting the user with a modal dialog.
- **Silent failures on the local log page.** A failed read discarded the exception and showed an empty box;
  a dead local variable meant the "no log for this date" case reported nothing; and a `logs.txt` fallback
  could display another day's content as if it were the selected date.
- **Serilog and the log viewer disagreed on the log location.** Serilog wrote relative to the working
  directory while the viewer read from the base directory. Both now use `AppContext.BaseDirectory`.
- Foreign project identifiers left in the template: namespace `AlgoFun.Infrastructures.Shell` and the
  fallback display names `"AlgoFun"` and `"Pitaya.Work"`, none of which `sourceName` substitution would
  have renamed in a scaffolded project.
- The default landing page was located with hard-coded `"Settings"` / `"Theme"` literals; it now uses
  `Constants.ThemeAppearance` and logs a warning instead of failing silently when that page is absent.
- `App.ServiceProvider` was a mutable, uninitialised non-nullable static; it now throws a diagnosable
  exception if read before the host finishes initializing.
- A startup failure left the process running with no window and no way to quit; it now reports the error and
  exits with a non-zero code.
- Nullable-reference correctness across the dialog infrastructure (`IDialogWindow.Result`, `Content`,
  `DataContext`, `Owner`, `Style`, and the `IDialogService` callback/parameter signatures), and
  `IDialogWindowExtensions.GetDialogViewModel` now throws a descriptive exception instead of an
  `InvalidCastException`.
- **`HostDialogService.ShowWindow` could never succeed.** It resolved keyed services with the *plural*
  `GetKeyedServices<UserControl>` and then required the result to be a `Window`, so it always threw. It now
  delegates to the inherited modal-window pipeline. The underlying reason it was unusable is also fixed:
  `HostDialogViewModel` now implements **both** `IDialogAware` and `IHostDialogAware`, so the same dialog
  view model works under the Material Design `DialogHost` and under a real window. `Cancel`/`Save` route
  through whichever host opened the dialog.
- **Security:** the native SQLite bundle resolved transitively at `SQLitePCLRaw.lib.e_sqlite3` 2.1.11,
  which carries [GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q) (high). Pinned to
  the patched 2.1.12, staying inside the same `major.minor` the SQLite packages are built against.

### Changed

- **Log levels cut back.** Release builds now record **Warning** and above; Debug builds record
  **Information** and above (previously Information / Debug). The `Microsoft` override moved from
  `Information` to `Warning` — at the old value the framework raised the volume in Release above the
  root minimum instead of capping it.
- **Central Package Management.** Every dependency version now lives in `Directory.Packages.props`;
  `PackageReference` entries carry no `Version`. This removes the per-csproj version drift.
- **All dependencies moved to released versions.** Previously the app pinned CI-feed and pre-release builds
  (`MaterialDesign* 5.3.3-ci1429`, `Volo.Abp* 10.6.0-rc.3`, `Microsoft.Data.Sqlite 11.0.0-preview.6`,
  `Microsoft.Web.WebView2 1.0.4126-prerelease`, `H.NotifyIcon.Wpf 2.5.0-dev.2`,
  `Serilog.Sinks.File 8.0.0-nblumhardt-02322`), and the publisher tool pinned
  `Microsoft.Extensions.Configuration 11.0.0-preview.6`. A `-ci` build disappears when its feed rotates,
  which would leave `dotnet new` output unable to restore. Now on 5.3.2 / 10.5.0 / 10.0.10 / 1.0.4078.44 /
  2.4.1 / 7.0.0 / 10.0.10 respectively.
- **Warnings are errors**, with .NET analyzers enabled. All 38 pre-existing warnings were fixed. Style/IDE
  rules are held at `suggestion` severity, and `NU1507` / `NU1901`-`NU1904` remain warnings so an
  environment quirk or a new advisory cannot fail a build without a source change.
- `[ObservableObject]` attribute usage replaced with inheritance from `ObservableObject` (MVVMTK0033);
  `ViewModelBase` now derives from it, and `HostDialogViewModel` owns the observable `Title` /
  `IdentifierName` rather than each derived dialog shadowing them.
- **Menu item templates deduplicated.** Three near-identical ~40-line `RadioButton` control templates in
  `MainWindow.xaml` collapsed into shared resources in `Themes/Navigation.xaml`. Per-item bindings
  (`IsChecked`, `Command`, `CommandParameter`, `GroupName`) are deliberately left as local values: moving
  them into style setters changes `RadioButton` group behaviour and the shell starts on the wrong page.
- `--EnableDesktopShortcut` defaults to **false**, so a scaffolded application no longer writes to the
  user's desktop on every launch.
- Template `sources.exclude` now also covers `artifacts/`, `.vs/`, `Logs/`, `*.db*`, `*.nupkg` and
  `secret.json`.
- **Repository-scoped feed pinning.** A root `NuGet.config` clears inherited sources and keeps only
  nuget.org, where every pinned package already resolves from. Restore is reproducible regardless of the
  machine's user-level feeds, and `NU1507` no longer fires on a repository build. Both `NuGet.config` and
  `global.json` are excluded from the template output (`.templateignore` and `sources.exclude`), so a
  scaffolded project still inherits whatever SDK and feeds its own environment provides.
- **Retargeted to `net10.0-windows`** (publisher tool and template pack to `net10.0`). The whole toolchain
  is now released: the build no longer needs a .NET 11 preview SDK, and CI installs `10.0.x` without
  `dotnet-quality: preview`.

### Known issues

- `ShowWindow` is exercised by unit tests at the view-model routing level, but no shipped page opens a
  window-hosted dialog, so that path has no end-to-end coverage yet.
- The `Themes/Controls/TabControl` region type is implemented and supported by `INavigationService`, but the
  shell still uses a single `ContentControl` region; the tabbed markup in `MainWindow.xaml` is commented out.

## [1.0.4]

- Baseline: Material Design WPF starter with ABP/Autofac DI, attribute-driven navigation, SQLite-backed
  theme preferences, Serilog file logging, an embedded Hangfire dashboard, tray icon and splash window,
  packaged as a `dotnet new` template with a NuGet publisher tool.
