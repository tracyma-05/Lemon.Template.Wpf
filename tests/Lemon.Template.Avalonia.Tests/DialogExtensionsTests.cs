using Lemon.Template.Avalonia.Commons;
using Lemon.Template.Avalonia.Infrastructures.Dialogs;
using Xunit;

namespace Lemon.Template.Avalonia.Tests;

/// <summary>
/// <c>Question("Title", "Message")</c> once bound to the (message, dialog identifier) overload and failed with
/// "No loaded DialogHost"; every two-string call must mean title and message on the root DialogHost.
/// </summary>
public sealed class DialogExtensionsTests
{
    [Fact]
    public async Task Two_strings_are_a_title_and_a_message()
    {
        var dialogs = new RecordingDialogs();

        Assert.True(await dialogs.Question("删除版本", "删除 1.2.0？"));

        Assert.Equal(Constants.RootIdentifier, dialogs.Identifier);
        Assert.Equal("删除版本", dialogs.Parameters!.GetValue<string>("Title"));
        Assert.Equal("删除 1.2.0？", dialogs.Parameters!.GetValue<string>("Message"));
    }

    [Fact]
    public async Task One_string_is_a_message_under_the_default_title()
    {
        var dialogs = new RecordingDialogs();

        await dialogs.Question("退出？");

        Assert.Equal(Constants.RootIdentifier, dialogs.Identifier);
        Assert.Equal("退出？", dialogs.Parameters!.GetValue<string>("Message"));
    }

    [Fact]
    public async Task Three_strings_pick_another_dialog_host()
    {
        var dialogs = new RecordingDialogs();

        await dialogs.Question("Title", "Message", "Settings");

        Assert.Equal("Settings", dialogs.Identifier);
    }

    private sealed class RecordingDialogs : IHostDialogService
    {
        public IDialogParameters? Parameters { get; private set; }

        public string? Identifier { get; private set; }

        public Task<IDialogResult> ShowDialogAsync(string name, IDialogParameters? parameters = null, string IdentifierName = "Root")
        {
            Parameters = parameters;
            Identifier = IdentifierName;
            return Task.FromResult<IDialogResult>(new DialogResult(ButtonResult.OK));
        }

        public void Close(string IdentifierName, DialogResult dialogResult) => throw new NotSupportedException();

        public void Show(string name, IDialogParameters? parameters, Action<IDialogResult>? callback) => throw new NotSupportedException();

        public void Show(string name, IDialogParameters? parameters, Action<IDialogResult>? callback, string? windowName) => throw new NotSupportedException();

        public Task<IDialogResult> ShowWindowAsync(string name, IDialogParameters? parameters = null, string? windowName = null) => throw new NotSupportedException();
    }
}
