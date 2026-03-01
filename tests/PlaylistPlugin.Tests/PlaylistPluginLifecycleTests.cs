using Moq;
using Vido.Core.Events;
using Vido.Core.Logging;
using Vido.Core.Playback;
using Vido.Core.Plugin;
using Xunit;

namespace PlaylistPlugin.Tests;

public class PlaylistPluginLifecycleTests
{
    [Fact]
    public void Activate_RegistersContributionsAndProvider()
    {
        var plugin = new PlaylistPlugin();

        var contextMock = new Mock<IPluginContext>();
        var engineMock = new Mock<IVideoEngine>();
        var eventBusMock = new Mock<IEventBus>();
        var settingsMock = new Mock<IPluginSettingsStore>();
        var loggerMock = new Mock<ILogService>();

        contextMock.SetupGet(c => c.VideoEngine).Returns(engineMock.Object);
        contextMock.SetupGet(c => c.Events).Returns(eventBusMock.Object);
        contextMock.SetupGet(c => c.Settings).Returns(settingsMock.Object);
        contextMock.SetupGet(c => c.Logger).Returns(loggerMock.Object);

        eventBusMock
            .Setup(e => e.Subscribe(It.IsAny<Action<VideoLoadedEvent>>()))
            .Returns(Mock.Of<IDisposable>());

        settingsMock.Setup(s => s.Get("recentPlaylists", string.Empty)).Returns(string.Empty);
        settingsMock.Setup(s => s.Get("lastPlaylistPath", string.Empty)).Returns(string.Empty);
        settingsMock.Setup(s => s.Get("autoSave", false)).Returns(false);

        plugin.Activate(contextMock.Object);

        contextMock.Verify(c => c.RegisterSidebarPanel("playlist-sidebar", It.IsAny<Func<object>>()), Times.Once);
        contextMock.Verify(c => c.RegisterStatusBarItem("playlist-status", It.IsAny<Func<object>>()), Times.Once);
        contextMock.Verify(c => c.RegisterContextMenuHandler("add-to-playlist", It.IsAny<Action<Vido.Core.FileSystem.FileNode>>()), Times.Once);
        contextMock.Verify(c => c.RegisterPlaylistProvider(It.IsAny<IPlaylistProvider>()), Times.Once);
        contextMock.Verify(c => c.UpdateStatusBarItem("playlist-status", It.IsAny<string>()), Times.AtLeastOnce);
        loggerMock.Verify(l => l.Info("Playlists plugin activated", "PlaylistPlugin"), Times.Once);
    }

    [Fact]
    public void Deactivate_AfterActivate_UnregistersProviderAndLogs()
    {
        var plugin = new PlaylistPlugin();

        var contextMock = new Mock<IPluginContext>();
        var engineMock = new Mock<IVideoEngine>();
        var eventBusMock = new Mock<IEventBus>();
        var settingsMock = new Mock<IPluginSettingsStore>();
        var loggerMock = new Mock<ILogService>();

        contextMock.SetupGet(c => c.VideoEngine).Returns(engineMock.Object);
        contextMock.SetupGet(c => c.Events).Returns(eventBusMock.Object);
        contextMock.SetupGet(c => c.Settings).Returns(settingsMock.Object);
        contextMock.SetupGet(c => c.Logger).Returns(loggerMock.Object);

        eventBusMock
            .Setup(e => e.Subscribe(It.IsAny<Action<VideoLoadedEvent>>()))
            .Returns(Mock.Of<IDisposable>());

        settingsMock.Setup(s => s.Get("recentPlaylists", string.Empty)).Returns(string.Empty);
        settingsMock.Setup(s => s.Get("lastPlaylistPath", string.Empty)).Returns(string.Empty);
        settingsMock.Setup(s => s.Get("autoSave", false)).Returns(false);

        plugin.Activate(contextMock.Object);

        plugin.Deactivate();

        contextMock.Verify(c => c.UnregisterPlaylistProvider(), Times.Once);
        loggerMock.Verify(l => l.Info("Playlists plugin deactivated", "PlaylistPlugin"), Times.Once);
    }

    [Fact]
    public void Deactivate_WithoutActivate_DoesNotThrow()
    {
        var plugin = new PlaylistPlugin();

        var exception = Record.Exception(plugin.Deactivate);

        Assert.Null(exception);
    }
}
