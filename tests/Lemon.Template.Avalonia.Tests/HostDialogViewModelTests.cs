using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lemon.Template.Avalonia.Infrastructures.Dialogs;
using Lemon.Template.Avalonia.Models;
using Xunit;

namespace Lemon.Template.Avalonia.Tests;

/// <summary>
/// Dialog view models have to work under both hosts: the Material Design DialogHost (closed through
/// <see cref="IHostDialogService.Close"/>) and a real modal window (closed through the RequestClose event
/// that <c>DialogService</c> subscribes). Getting this wrong makes one of the two hosting paths dead.
/// </summary>
public class HostDialogViewModelTests
{
    [Fact]
    public void ViewModel_SatisfiesBothDialogContracts()
    {
        var viewModel = new TestDialogViewModel(new RecordingHostDialogService());

        Assert.IsAssignableFrom<IHostDialogAware>(viewModel);
        Assert.IsAssignableFrom<IDialogAware>(viewModel);
    }

    [Fact]
    public void Cancel_WithoutAWindowHost_ClosesThroughTheDialogHost()
    {
        var host = new RecordingHostDialogService();
        var viewModel = new TestDialogViewModel(host) { IdentifierName = "Root" };

        viewModel.Cancel();

        var (identifier, result) = Assert.Single(host.Closed);
        Assert.Equal("Root", identifier);
        Assert.Equal(ButtonResult.No, result.Result);
    }

    [Fact]
    public void Cancel_WithAWindowHost_RaisesRequestCloseInsteadOfTouchingTheDialogHost()
    {
        var host = new RecordingHostDialogService();
        var viewModel = new TestDialogViewModel(host);
        IDialogResult? raised = null;
        viewModel.RequestClose += r => raised = r;

        viewModel.Cancel();

        Assert.NotNull(raised);
        Assert.Equal(ButtonResult.No, raised!.Result);
        Assert.Empty(host.Closed);
    }

    [Fact]
    public async Task Save_WithAWindowHost_RaisesRequestCloseWithOk()
    {
        var host = new RecordingHostDialogService();
        var viewModel = new TestDialogViewModel(host);
        IDialogResult? raised = null;
        viewModel.RequestClose += r => raised = r;

        await viewModel.Save();

        Assert.NotNull(raised);
        Assert.Equal(ButtonResult.OK, raised!.Result);
        Assert.Empty(host.Closed);
    }

    [Fact]
    public void CanCloseDialog_DefaultsToTrue()
    {
        var viewModel = new TestDialogViewModel(new RecordingHostDialogService());

        Assert.True(viewModel.CanCloseDialog());
    }

    private sealed class TestDialogViewModel(IHostDialogService dialogService)
        : HostDialogViewModel(dialogService)
    {
        public override void OnDialogOpened(IDialogParameters parameters)
        {
        }
    }

    private sealed class RecordingHostDialogService : IHostDialogService
    {
        public List<(string Identifier, DialogResult Result)> Closed { get; } = [];

        public void Close(string IdentifierName, DialogResult dialogResult) =>
            Closed.Add((IdentifierName, dialogResult));

        public Task<IDialogResult> ShowDialogAsync(string name, IDialogParameters? parameters = null, string IdentifierName = "Root") =>
            throw new NotSupportedException();

        public void Show(string name, IDialogParameters? parameters, Action<IDialogResult>? callback) =>
            throw new NotSupportedException();

        public void Show(string name, IDialogParameters? parameters, Action<IDialogResult>? callback, string? windowName) =>
            throw new NotSupportedException();

        public Task<IDialogResult> ShowWindowAsync(string name, IDialogParameters? parameters = null, string? windowName = null) =>
            throw new NotSupportedException();
    }
}
