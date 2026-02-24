using System.IO;
using Moq;
using PlaylistPlugin.Services;
using PlaylistPlugin.ViewModels;
using Vido.Core.Events;
using Vido.Core.Playback;
using Xunit;

namespace PlaylistPlugin.Tests;

public class PlaylistViewModelTests : IDisposable
{
    private readonly Mock<IVideoEngine> _engineMock;
    private readonly Mock<IEventBus> _eventBusMock;
    private readonly PlaylistFileService _fileService;
    private readonly PlaylistViewModel _vm;
    private readonly string _tempDir;

    // Capture the VideoLoadedEvent handler registered via Subscribe
    private Action<VideoLoadedEvent>? _videoLoadedHandler;

    public PlaylistViewModelTests()
    {
        _engineMock = new Mock<IVideoEngine>();
        _eventBusMock = new Mock<IEventBus>();

        // Capture the subscription handler so tests can invoke it
        _eventBusMock
            .Setup(e => e.Subscribe(It.IsAny<Action<VideoLoadedEvent>>()))
            .Callback<Action<VideoLoadedEvent>>(handler => _videoLoadedHandler = handler)
            .Returns(Mock.Of<IDisposable>());

        _fileService = new PlaylistFileService();
        _vm = new PlaylistViewModel(_fileService, _engineMock.Object, _eventBusMock.Object);

        _tempDir = Path.Combine(Path.GetTempPath(), "PlaylistVmTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        _vm.Dispose();
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    // ── Constructor ──

    [Fact]
    public void Constructor_CreatesDefaultPlaylist()
    {
        Assert.NotNull(_vm.CurrentPlaylist);
        Assert.Equal("Untitled Playlist", _vm.PlaylistName);
        Assert.Empty(_vm.Items);
        Assert.False(_vm.HasItems);
    }

    [Fact]
    public void Constructor_ThrowsOnNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PlaylistViewModel(null!, _engineMock.Object, _eventBusMock.Object));
        Assert.Throws<ArgumentNullException>(() =>
            new PlaylistViewModel(_fileService, null!, _eventBusMock.Object));
        Assert.Throws<ArgumentNullException>(() =>
            new PlaylistViewModel(_fileService, _engineMock.Object, null!));
    }

    // ── AddItem ──

    [Fact]
    public void AddItem_AddsToItemsCollection()
    {
        _vm.AddItem(@"C:\Videos\video1.mp4");

        Assert.Single(_vm.Items);
        Assert.Equal("video1.mp4", _vm.Items[0].FileName);
        Assert.True(_vm.HasItems);
    }

    [Fact]
    public void AddItem_SkipsDuplicates()
    {
        _vm.AddItem(@"C:\Videos\video1.mp4");
        _vm.AddItem(@"C:\Videos\video1.mp4");
        _vm.AddItem(@"c:\videos\VIDEO1.MP4"); // case-insensitive duplicate

        Assert.Single(_vm.Items);
    }

    [Fact]
    public void AddItem_ThrowsOnNullOrWhiteSpace()
    {
        Assert.ThrowsAny<ArgumentException>(() => _vm.AddItem(null!));
        Assert.ThrowsAny<ArgumentException>(() => _vm.AddItem(""));
        Assert.ThrowsAny<ArgumentException>(() => _vm.AddItem("   "));
    }

    // ── AddItems ──

    [Fact]
    public void AddItems_AddsMultipleFiles()
    {
        _vm.AddItems([@"C:\Videos\a.mp4", @"C:\Videos\b.mp4", @"C:\Videos\c.mp4"]);

        Assert.Equal(3, _vm.Items.Count);
    }

    [Fact]
    public void AddItems_SkipsDuplicatesAcrossBatch()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItems([@"C:\Videos\a.mp4", @"C:\Videos\b.mp4"]);

        Assert.Equal(2, _vm.Items.Count);
    }

    // ── HandleFileDrop ──

    [Fact]
    public void HandleFileDrop_AddsDroppedFiles()
    {
        // Create real temp files
        var file1 = Path.Combine(_tempDir, "video1.mp4");
        var file2 = Path.Combine(_tempDir, "video2.mp4");
        File.WriteAllText(file1, "fake");
        File.WriteAllText(file2, "fake");

        _vm.HandleFileDrop([file1, file2]);

        Assert.Equal(2, _vm.Items.Count);
    }

    [Fact]
    public void HandleFileDrop_RecursivelyScansDroppedFolders()
    {
        // Create nested folder structure
        var subDir = Path.Combine(_tempDir, "subfolder");
        Directory.CreateDirectory(subDir);

        File.WriteAllText(Path.Combine(_tempDir, "root.mp4"), "fake");
        File.WriteAllText(Path.Combine(subDir, "nested.mp4"), "fake");

        _vm.HandleFileDrop([_tempDir]);

        Assert.Equal(2, _vm.Items.Count);
    }

    [Fact]
    public void HandleFileDrop_IgnoresNonexistentPaths()
    {
        _vm.HandleFileDrop([@"C:\NonExistent\does_not_exist_12345.mp4"]);

        Assert.Empty(_vm.Items);
    }

    // ── NewPlaylistCommand ──

    [Fact]
    public void NewPlaylistCommand_ClearsItemsAndResetsName()
    {
        _vm.AddItem(@"C:\Videos\video1.mp4");
        _vm.AddItem(@"C:\Videos\video2.mp4");
        Assert.Equal(2, _vm.Items.Count);

        _vm.NewPlaylistCommand.Execute(null);

        Assert.Empty(_vm.Items);
        Assert.Equal("Untitled Playlist", _vm.PlaylistName);
        Assert.Null(_vm.CurrentItem);
    }

    // ── CurrentItem (Selection + Playing Indicator) ──

    [Fact]
    public void CurrentItem_SetsIsPlayingOnSelectedItem()
    {
        _vm.AddItem(@"C:\Videos\video1.mp4");
        _vm.AddItem(@"C:\Videos\video2.mp4");

        _vm.CurrentItem = _vm.Items[0];

        Assert.True(_vm.Items[0].IsPlaying);
        Assert.False(_vm.Items[1].IsPlaying);
    }

    [Fact]
    public void CurrentItem_ClearsPreviousIsPlaying()
    {
        _vm.AddItem(@"C:\Videos\video1.mp4");
        _vm.AddItem(@"C:\Videos\video2.mp4");

        _vm.CurrentItem = _vm.Items[0];
        _vm.CurrentItem = _vm.Items[1];

        Assert.False(_vm.Items[0].IsPlaying);
        Assert.True(_vm.Items[1].IsPlaying);
    }

    // ── PlayItemCommand ──

    [Fact]
    public void PlayItemCommand_PublishesPlayFileRequestedEvent()
    {
        // Create a real temp file so FileExists returns true
        var filePath = Path.Combine(_tempDir, "test.mp4");
        File.WriteAllText(filePath, "fake");

        _vm.AddItem(filePath);
        var item = _vm.Items[0];

        _vm.PlayItemCommand.Execute(item);

        _eventBusMock.Verify(e => e.Publish(It.Is<PlayFileRequestedEvent>(
            evt => evt.FilePath == filePath)), Times.Once);
    }

    [Fact]
    public void PlayItemCommand_DoesNotPlayMissingFile()
    {
        _vm.AddItem(@"C:\NonExistent\does_not_exist_12345.mp4");
        var item = _vm.Items[0];

        _vm.PlayItemCommand.Execute(item);

        _eventBusMock.Verify(e => e.Publish(It.IsAny<PlayFileRequestedEvent>()), Times.Never);
    }

    // ── VideoLoadedEvent tracking ──

    [Fact]
    public void OnVideoLoaded_SetsCurrentItemToMatchingFile()
    {
        _vm.AddItem(@"C:\Videos\video1.mp4");
        _vm.AddItem(@"C:\Videos\video2.mp4");

        Assert.NotNull(_videoLoadedHandler);

        _videoLoadedHandler!(new VideoLoadedEvent
        {
            FilePath = @"C:\Videos\video2.mp4",
            Metadata = new VideoMetadata
            {
                FilePath = @"C:\Videos\video2.mp4",
                FileName = "video2.mp4",
                Width = 1920,
                Height = 1080,
                Duration = TimeSpan.FromMinutes(5)
            }
        });

        Assert.Equal(_vm.Items[1], _vm.CurrentItem);
        Assert.True(_vm.Items[1].IsPlaying);
    }

    [Fact]
    public void OnVideoLoaded_ClearsCurrentItemIfNoMatch()
    {
        _vm.AddItem(@"C:\Videos\video1.mp4");
        _vm.CurrentItem = _vm.Items[0];

        Assert.NotNull(_videoLoadedHandler);

        _videoLoadedHandler!(new VideoLoadedEvent
        {
            FilePath = @"C:\Videos\other.mp4",
            Metadata = new VideoMetadata
            {
                FilePath = @"C:\Videos\other.mp4",
                FileName = "other.mp4",
                Width = 1920,
                Height = 1080,
                Duration = TimeSpan.FromMinutes(5)
            }
        });

        Assert.Null(_vm.CurrentItem);
    }

    // ── PlaylistName ──

    [Fact]
    public void PlaylistName_UpdatesModelNameAndRaisesPropertyChanged()
    {
        var raised = false;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlaylistViewModel.PlaylistName))
                raised = true;
        };

        _vm.PlaylistName = "My Favorite Videos";

        Assert.Equal("My Favorite Videos", _vm.PlaylistName);
        Assert.Equal("My Favorite Videos", _vm.CurrentPlaylist.Name);
        Assert.True(raised);
    }

    // ── HasItems ──

    [Fact]
    public void HasItems_RaisesPropertyChangedWhenItemsChange()
    {
        var hasItemsValues = new List<bool>();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlaylistViewModel.HasItems))
                hasItemsValues.Add(_vm.HasItems);
        };

        _vm.AddItem(@"C:\Videos\video1.mp4");

        Assert.Contains(true, hasItemsValues);
    }

    // ── AddFromFileNode (Context Menu) ──

    [Fact]
    public void AddFromFileNode_File_AddsToPlaylist()
    {
        var filePath = Path.Combine(_tempDir, "video.mp4");
        File.WriteAllText(filePath, "fake");

        _vm.AddFromFileNode(filePath, isDirectory: false);

        Assert.Single(_vm.Items);
        Assert.Equal("video.mp4", _vm.Items[0].FileName);
    }

    [Fact]
    public void AddFromFileNode_Directory_RecursivelyAddsAllFiles()
    {
        var subDir = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(_tempDir, "root.mp4"), "fake");
        File.WriteAllText(Path.Combine(subDir, "nested.mp4"), "fake");

        _vm.AddFromFileNode(_tempDir, isDirectory: true);

        Assert.Equal(2, _vm.Items.Count);
    }

    [Fact]
    public void AddFromFileNode_File_SkipsDuplicates()
    {
        _vm.AddItem(@"C:\Videos\video1.mp4");

        _vm.AddFromFileNode(@"C:\Videos\video1.mp4", isDirectory: false);

        Assert.Single(_vm.Items);
    }

    [Fact]
    public void AddFromFileNode_File_SetsDirtyFlag()
    {
        var filePath = Path.Combine(_tempDir, "video.mp4");
        File.WriteAllText(filePath, "fake");
        _vm.CurrentPlaylist.IsDirty = false;

        _vm.AddFromFileNode(filePath, isDirectory: false);

        Assert.True(_vm.CurrentPlaylist.IsDirty);
    }

    [Fact]
    public void AddFromFileNode_Directory_SkipsDuplicatesAcrossExisting()
    {
        var filePath = Path.Combine(_tempDir, "video.mp4");
        File.WriteAllText(filePath, "fake");
        _vm.AddItem(filePath);

        _vm.AddFromFileNode(_tempDir, isDirectory: true);

        Assert.Single(_vm.Items);
    }

    // ── Dispose ──

    [Fact]
    public void Dispose_DisposesEventSubscription()
    {
        var subscription = new Mock<IDisposable>();
        var eventBus = new Mock<IEventBus>();
        eventBus
            .Setup(e => e.Subscribe(It.IsAny<Action<VideoLoadedEvent>>()))
            .Returns(subscription.Object);

        var vm = new PlaylistViewModel(_fileService, _engineMock.Object, eventBus.Object);
        vm.Dispose();

        subscription.Verify(s => s.Dispose(), Times.Once);
    }
}
