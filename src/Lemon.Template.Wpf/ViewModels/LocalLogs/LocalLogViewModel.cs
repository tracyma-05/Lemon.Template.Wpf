using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Text;
using Volo.Abp.DependencyInjection;

namespace Lemon.Template.Wpf.ViewModels.LocalLogs;

[ObservableObject]
public partial class LocalLogViewModel : ITransientDependency
{
    private const int MaxDisplayBytes = 512 * 1024;

    public LocalLogViewModel()
    {
        SelectedDate = DateTime.Today;
    }

    [ObservableProperty]
    private DateTime? _selectedDate;

    [ObservableProperty]
    private string _logContent = string.Empty;

    [RelayCommand]
    private async Task LoadAsync()
    {
        var path = ResolveLogFilePath();
        if (path is null)
        {
            LogContent = string.Empty;
            var day = SelectedDate ?? DateTime.Today;
            
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
            string prefix = string.Empty;
            if (length > MaxDisplayBytes)
            {
                stream.Seek(length - MaxDisplayBytes, SeekOrigin.Begin);
                prefix = $"(Truncated: showing last {MaxDisplayBytes / 1024} KB of ~{length / 1024} KB)\r\n\r\n";
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var body = await reader.ReadToEndAsync().ConfigureAwait(false);
            LogContent = prefix + body;
        }
        catch (IOException ex)
        {
            LogContent = string.Empty;
        }
    }

    private static string LogsDirectory =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

    private string? ResolveLogFilePath()
    {
        var dir = LogsDirectory;
        if (!Directory.Exists(dir))
            return null;

        var day = SelectedDate ?? DateTime.Today;
        var datedName = $"log-{day:yyyyMMdd}.txt";
        var datedPath = Path.Combine(dir, datedName);
        if (File.Exists(datedPath))
            return datedPath;

        var legacy = Path.Combine(dir, "logs.txt");
        if (File.Exists(legacy))
            return legacy;

        return null;
    }
}
