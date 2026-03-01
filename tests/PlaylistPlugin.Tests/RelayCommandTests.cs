using PlaylistPlugin.ViewModels;
using Xunit;

namespace PlaylistPlugin.Tests;

public class RelayCommandTests
{
    [Fact]
    public void RelayCommand_Execute_InvokesAction()
    {
        var invoked = false;
        var command = new RelayCommand(() => invoked = true);

        command.Execute(null);

        Assert.True(invoked);
    }

    [Fact]
    public void RelayCommandT_Execute_WithMatchingType_InvokesActionWithValue()
    {
        string? captured = null;
        var command = new RelayCommand<string>(value => captured = value);

        command.Execute("value");

        Assert.Equal("value", captured);
    }

    [Fact]
    public void RelayCommandT_Execute_WithMismatchedType_PassesDefault()
    {
        string? captured = "initial";
        var command = new RelayCommand<string>(value => captured = value);

        command.Execute(123);

        Assert.Null(captured);
    }
}
