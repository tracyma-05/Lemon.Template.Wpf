using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemon.Template.Wpf.Infrastructures.Localization;
using Serilog;
using System.IO;
using System.Text;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Wpf.ViewModels.LocalLogs;

public partial class LocalLogViewModel : ObservableObject, ITransientDependency
{
    private const int MaxDisplayBytes = 512 * 1024;

    private readonly ILocalizationService _localization;

    public LocalLogViewModel(ILocalizationService localization)
    {
        _localization = localization;
        SelectedDate = DateTime.Today;
    }

    [ObservableProperty]
    private DateTime? _selectedDate;

    [ObservableProperty]
    private string _logContent = string.Empty;

    /// <summary>读取结果说明（文件路径 / 截断提示 / 失败原因），避免只留一片空白。</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [RelayCommand]
    private async Task LoadAsync()
    {
        var day = SelectedDate ?? DateTime.Today;
        var path = ResolveLogFilePath(day);
        if (path is null)
        {
            LogContent = string.Empty;
            StatusMessage = _localization.Format("LocalLog_NoFileForDate", $"{day:yyyy-MM-dd}", LogsDirectory);
            return;
        }

        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);

            var length = stream.Length;
            var truncated = length > MaxDisplayBytes;
            if (truncated)
            {
                stream.Seek(length - MaxDisplayBytes, SeekOrigin.Begin);
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            // 不用 ConfigureAwait(false)：后续赋值是可观察属性，需要回到 UI 线程。
            LogContent = await reader.ReadToEndAsync();
            StatusMessage = truncated
                ? _localization.Format("LocalLog_Truncated", path, MaxDisplayBytes / 1024, length / 1024)
                : _localization.Format("LocalLog_Loaded", path, length / 1024);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Failed to read log file {Path}.", path);
            LogContent = string.Empty;
            StatusMessage = _localization.Format("LocalLog_ReadFailed", path, ex.Message);
        }
    }

    private static string LogsDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Logs");

    private static string? ResolveLogFilePath(DateTime day)
    {
        // 只认所选日期对应的文件：回退到别的文件会把其它日期的内容当成当天显示。
        var path = Path.Combine(LogsDirectory, $"log-{day:yyyyMMdd}.txt");
        return File.Exists(path) ? path : null;
    }
}
