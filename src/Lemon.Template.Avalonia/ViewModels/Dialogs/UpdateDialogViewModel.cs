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
    /// Shows the outcome of an update check. <see cref="HostDialogViewModel.Save()"/> (the primary
    /// button) means "download"; it is only offered when a newer version exists.
    /// </summary>
    public sealed partial class UpdateDialogViewModel : HostDialogViewModel, ITransientDependency
    {
        public const string ResultParameter = "Result";

        private readonly ILocalizationService _localization;

        public UpdateDialogViewModel(IHostDialogService dialogService, ILocalizationService localization)
            : base(dialogService)
        {
            _localization = localization;
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

        public override void OnDialogOpened(IDialogParameters parameters)
        {
            if (!parameters.ContainsKey(ResultParameter))
            {
                return;
            }

            var result = parameters.GetValue<UpdateCheckResult>(ResultParameter);

            CurrentVersion = result.CurrentVersion.ToString(3);
            LatestVersion = result.Latest?.Version.ToString(3) ?? "—";
            PublishedAt = result.Latest?.PublishedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", _localization.CurrentCulture);
            ReleaseNotes = string.IsNullOrWhiteSpace(result.Latest?.ReleaseNotes) ? null : result.Latest!.ReleaseNotes!.Trim();
            IsUpdateAvailable = result.Status == UpdateCheckStatus.UpdateAvailable;

            switch (result.Status)
            {
                case UpdateCheckStatus.UpdateAvailable:
                    Icon = MaterialIconKind.ArrowUpBoldCircleOutline;
                    Title = _localization.GetString("Update_Available_Title");
                    Message = _localization.Format("Update_Available_Message", LatestVersion);
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
    }
}
