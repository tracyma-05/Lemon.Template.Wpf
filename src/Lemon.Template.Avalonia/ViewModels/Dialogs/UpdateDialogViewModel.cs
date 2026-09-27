using CommunityToolkit.Mvvm.ComponentModel;
using Lemon.Template.Avalonia.Infrastructures.Dialogs;
using Lemon.Template.Avalonia.Infrastructures.Localization;
using Lemon.Template.Avalonia.Models;
using Lemon.Template.Avalonia.Services.Updates;
using Material.Icons;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Avalonia.ViewModels.Dialogs
{
    /// <summary>
    /// Shows the outcome of an update check. The primary button (<see cref="Save()"/>) is only offered when a
    /// newer version exists: "Update and restart" downloads and unpacks it here, with progress, and closes with
    /// <see cref="PreparedParameter"/> for the shell to hand over to the updater; "Download" (an installer, a
    /// read-only app folder, or after a failed attempt) closes with plain OK and the shell opens the browser.
    /// </summary>
    public sealed partial class UpdateDialogViewModel : HostDialogViewModel, ITransientDependency
    {
        public const string ResultParameter = "Result";

        /// <summary>Set on an OK result when the update is ready to install (a <see cref="PreparedUpdate"/>).</summary>
        public const string PreparedParameter = "Prepared";

        private readonly ILocalizationService _localization;
        private readonly IUpdateInstaller _installer;
        private UpdateCheckResult? _result;
        private CancellationTokenSource? _installCancellation;

        public UpdateDialogViewModel(IHostDialogService dialogService, ILocalizationService localization, IUpdateInstaller installer)
            : base(dialogService)
        {
            _localization = localization;
            _installer = installer;
        }

        [ObservableProperty]
        private string _message = string.Empty;

        [ObservableProperty]
        private MaterialIconKind _icon = MaterialIconKind.Update;

        [ObservableProperty]
        private bool _isUpdateAvailable;

        [ObservableProperty]
        private string _currentVersion = string.Empty;

        [ObservableProperty]
        private string _latestVersion = string.Empty;

        [ObservableProperty]
        private string? _publishedAt;

        [ObservableProperty]
        private string? _releaseNotes;

        /// <summary>The primary button installs in place rather than opening the download.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PrimaryButtonText))]
        private bool _canInstall;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SecondaryButtonText))]
        private bool _isInstalling;

        /// <summary>0–100; meaningless while <see cref="IsProgressIndeterminate"/>.</summary>
        [ObservableProperty]
        private double _progress;

        [ObservableProperty]
        private bool _isProgressIndeterminate = true;

        [ObservableProperty]
        private string? _progressText;

        /// <summary>Why the automatic install failed; the primary button then offers the manual download.</summary>
        [ObservableProperty]
        private string? _installError;

        public string PrimaryButtonText => _localization.GetString(CanInstall ? "Update_Install" : "Update_Download");

        public string SecondaryButtonText => _localization.GetString(IsInstalling ? "Update_Cancel" : "Update_Later");

        public override void OnDialogOpened(IDialogParameters parameters)
        {
            if (!parameters.ContainsKey(ResultParameter))
            {
                return;
            }

            var result = parameters.GetValue<UpdateCheckResult>(ResultParameter);
            _result = result;

            CurrentVersion = result.CurrentVersion.ToString(3);
            LatestVersion = result.Latest?.Version.ToString(3) ?? "—";
            PublishedAt = result.Latest?.PublishedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", _localization.CurrentCulture);
            ReleaseNotes = string.IsNullOrWhiteSpace(result.Latest?.ReleaseNotes) ? null : result.Latest!.ReleaseNotes!.Trim();
            IsUpdateAvailable = result.Status == UpdateCheckStatus.UpdateAvailable;
            CanInstall = IsUpdateAvailable && _installer.CanInstall(result);

            switch (result.Status)
            {
                case UpdateCheckStatus.UpdateAvailable:
                    Icon = MaterialIconKind.ArrowUpBoldCircleOutline;
                    Title = _localization.GetString("Update_Available_Title");
                    Message = _localization.Format(CanInstall ? "Update_Available_Message_Install" : "Update_Available_Message", LatestVersion);
                    break;
                case UpdateCheckStatus.UpToDate:
                    Icon = MaterialIconKind.CheckCircleOutline;
                    Title = _localization.GetString("Update_UpToDate_Title");
                    Message = _localization.Format("Update_UpToDate_Message", CurrentVersion);
                    break;
                default:
                    Icon = MaterialIconKind.AlertCircleOutline;
                    Title = _localization.GetString("Update_Failed_Title");
                    Message = result.Error ?? string.Empty;
                    break;
            }
        }

        public override async Task Save()
        {
            if (!CanInstall || _result is null)
            {
                await base.Save(); // plain OK: the shell opens the download in the browser
                return;
            }

            InstallError = null;
            IsInstalling = true;
            IsProgressIndeterminate = true;
            ProgressText = _localization.GetString("Update_Downloading");
            _installCancellation = new CancellationTokenSource();
            try
            {
                var progress = new Progress<UpdateDownloadProgress>(OnDownloadProgress);
                var prepared = await _installer.PrepareAsync(_result, progress, _installCancellation.Token);

                ProgressText = _localization.GetString("Update_Restarting");
                Save(new DialogParameters { { PreparedParameter, prepared } });
            }
            catch (OperationCanceledException)
            {
                // Cancel already closed the dialog.
            }
            catch (UpdateInstallException ex)
            {
                InstallError = ex.Message;
                Message = _localization.Format("Update_Available_Message", LatestVersion);
                CanInstall = false; // the next click opens the manual download
            }
            finally
            {
                IsInstalling = false;
                _installCancellation?.Dispose();
                _installCancellation = null;
            }
        }

        public override void Cancel()
        {
            _installCancellation?.Cancel();
            base.Cancel();
        }

        /// <summary>Closed some other way (click-away, Escape) while downloading: stop the download too.</summary>
        public override void OnDialogClosed()
        {
            _installCancellation?.Cancel();
            base.OnDialogClosed();
        }

        private void OnDownloadProgress(UpdateDownloadProgress value)
        {
            if (value.Fraction is { } fraction)
            {
                IsProgressIndeterminate = false;
                Progress = fraction * 100;
                ProgressText = _localization.Format("Update_DownloadingProgress", (int)Math.Round(Progress),
                    FormatSize(value.Received), FormatSize(value.Total!.Value));
            }
            else
            {
                ProgressText = _localization.Format("Update_DownloadingBytes", FormatSize(value.Received));
            }
        }

        private static string FormatSize(long bytes) => bytes >= 1024 * 1024
            ? $"{bytes / 1024d / 1024d:0.0} MB"
            : $"{Math.Max(1, bytes / 1024)} KB";
    }
}
