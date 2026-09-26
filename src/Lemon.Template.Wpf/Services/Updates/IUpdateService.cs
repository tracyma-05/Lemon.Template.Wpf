namespace Lemon.Template.Wpf.Services.Updates;

public interface IUpdateService
{
    /// <summary>True when <c>Update:Enabled</c> is on and <c>Update:Url</c> is a usable http(s) address.</summary>
    bool IsEnabled { get; }

    /// <summary>True when the shell should check quietly after start-up.</summary>
    bool CheckOnStartup { get; }

    /// <summary>The running application's version (the project's <c>Version</c> property).</summary>
    Version CurrentVersion { get; }

    /// <summary>Fetches the manifest and compares it with <see cref="CurrentVersion"/>. Never throws for network or format problems.</summary>
    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default);
}
