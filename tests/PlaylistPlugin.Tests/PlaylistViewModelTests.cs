using System.IO;
using Moq;
using PlaylistPlugin.Services;
using PlaylistPlugin.ViewModels;
using Vido.Core.Events;
using Vido.Core.Playback;
using Vido.Core.Plugin;
using Xunit;

namespace PlaylistPlugin.Tests;

public class PlaylistViewModelTests : IDisposable
{
    private readonly Mock<IVideoEngine> _engineMock;
    private readonly Mock<IEventBus> _eventBusMock;
    private readonly Mock<IDialogService> _dialogMock;
    private readonly Mock<IPluginSettingsStore> _settingsMock;
    private readonly PlaylistFileService _fileService;
    private readonly PlaylistViewModel _vm;
    private readonly string _tempDir;
    private readonly List<string> _statusBarUpdates;

    // Capture the VideoLoadedEvent handler registered via Subscribe
    private Action<VideoLoadedEvent>? _videoLoadedHandler;

    public PlaylistViewModelTests()
    {
        _engineMock = new Mock<IVideoEngine>();
        _eventBusMock = new Mock<IEventBus>();
        _dialogMock = new Mock<IDialogService>();
        _settingsMock = new Mock<IPluginSettingsStore>();
        _statusBarUpdates = [];

        // Capture the subscription handler so tests can invoke it
        _eventBusMock
            .Setup(e => e.Subscribe(It.IsAny<Action<VideoLoadedEvent>>()))
            .Callback<Action<VideoLoadedEvent>>(handler => _videoLoadedHandler = handler)
            .Returns(Mock.Of<IDisposable>());

        // Return empty for recent playlists by default
        _settingsMock.Setup(s => s.Get("recentPlaylists", string.Empty)).Returns(string.Empty);

        _fileService = new PlaylistFileService();
        _vm = new PlaylistViewModel(
            _fileService,
            _engineMock.Object,
            _eventBusMock.Object,
            _dialogMock.Object,
            _settingsMock.Object,
            text => _statusBarUpdates.Add(text));

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
            new PlaylistViewModel(null!, _engineMock.Object, _eventBusMock.Object, _dialogMock.Object));
        Assert.Throws<ArgumentNullException>(() =>
            new PlaylistViewModel(_fileService, null!, _eventBusMock.Object, _dialogMock.Object));
        Assert.Throws<ArgumentNullException>(() =>
            new PlaylistViewModel(_fileService, _engineMock.Object, null!, _dialogMock.Object));
        Assert.Throws<ArgumentNullException>(() =>
            new PlaylistViewModel(_fileService, _engineMock.Object, _eventBusMock.Object, null!));
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

    [Fact]
    public void AddItem_IgnoresNonVideoFiles()
    {
        _vm.AddItem(@"C:\Files\document.txt");
        _vm.AddItem(@"C:\Files\image.png");
        _vm.AddItem(@"C:\Files\script.funscript");

        Assert.Empty(_vm.Items);
    }

    [Theory]
    [InlineData(".mp4")]
    [InlineData(".avi")]
    [InlineData(".mkv")]
    [InlineData(".mov")]
    [InlineData(".wmv")]
    [InlineData(".flv")]
    [InlineData(".webm")]
    public void AddItem_AcceptsAllSupportedVideoExtensions(string ext)
    {
        _vm.AddItem($@"C:\Videos\video{ext}");

        Assert.Single(_vm.Items);
    }

    [Fact]
    public void IsVideoFile_ReturnsFalseForNonVideoExtensions()
    {
        Assert.False(PlaylistViewModel.IsVideoFile(@"C:\Files\doc.txt"));
        Assert.False(PlaylistViewModel.IsVideoFile(@"C:\Files\pic.png"));
        Assert.False(PlaylistViewModel.IsVideoFile(@"C:\Files\script.funscript"));
        Assert.False(PlaylistViewModel.IsVideoFile(@"C:\Files\noext"));
    }

    [Fact]
    public void IsVideoFile_IsCaseInsensitive()
    {
        Assert.True(PlaylistViewModel.IsVideoFile(@"C:\Videos\clip.MP4"));
        Assert.True(PlaylistViewModel.IsVideoFile(@"C:\Videos\clip.Mkv"));
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
    public void HandleFileDrop_SkipsNonVideoFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "video.mp4"), "fake");
        File.WriteAllText(Path.Combine(_tempDir, "readme.txt"), "fake");
        File.WriteAllText(Path.Combine(_tempDir, "image.png"), "fake");
        File.WriteAllText(Path.Combine(_tempDir, "script.funscript"), "fake");

        _vm.HandleFileDrop([_tempDir]);

        Assert.Single(_vm.Items);
        Assert.Equal("video.mp4", _vm.Items[0].FileName);
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

        // User chooses "No" (discard changes) when prompted
        _dialogMock.Setup(d => d.ShowConfirmationDialog(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

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

        var vm = new PlaylistViewModel(_fileService, _engineMock.Object, eventBus.Object, _dialogMock.Object);
        vm.Dispose();

        subscription.Verify(s => s.Dispose(), Times.Once);
    }

    // ── SavePlaylistCommand ──

    [Fact]
    public async Task SavePlaylistCommand_PromptsDialogWhenNoPath()
    {
        var savePath = Path.Combine(_tempDir, "saved.vidpl");
        _dialogMock.Setup(d => d.ShowSaveFileDialog(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(savePath);

        _vm.AddItem(@"C:\Videos\video1.mp4");

        var result = await _vm.SaveCurrentPlaylistAsync(saveAs: false);

        Assert.True(result);
        _dialogMock.Verify(d => d.ShowSaveFileDialog(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        Assert.True(File.Exists(savePath));
        Assert.False(_vm.CurrentPlaylist.IsDirty);
    }

    [Fact]
    public async Task SavePlaylistCommand_SavesDirectlyWhenPathExists()
    {
        var savePath = Path.Combine(_tempDir, "existing.vidpl");
        _vm.CurrentPlaylist.FilePath = savePath;
        _vm.AddItem(@"C:\Videos\video1.mp4");

        var result = await _vm.SaveCurrentPlaylistAsync(saveAs: false);

        Assert.True(result);
        _dialogMock.Verify(d => d.ShowSaveFileDialog(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        Assert.True(File.Exists(savePath));
    }

    [Fact]
    public async Task SavePlaylistAsCommand_AlwaysPromptsDialog()
    {
        var savePath = Path.Combine(_tempDir, "saveas.vidpl");
        _vm.CurrentPlaylist.FilePath = Path.Combine(_tempDir, "existing.vidpl");

        _dialogMock.Setup(d => d.ShowSaveFileDialog(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(savePath);

        _vm.AddItem(@"C:\Videos\video1.mp4");

        var result = await _vm.SaveCurrentPlaylistAsync(saveAs: true);

        Assert.True(result);
        _dialogMock.Verify(d => d.ShowSaveFileDialog(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SavePlaylistCommand_ReturnsFalseWhenCancelled()
    {
        _dialogMock.Setup(d => d.ShowSaveFileDialog(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string?)null);

        var result = await _vm.SaveCurrentPlaylistAsync(saveAs: false);

        Assert.False(result);
    }

    // ── OpenPlaylistCommand ──

    [Fact]
    public async Task LoadPlaylistFromPath_LoadsPlaylistCorrectly()
    {
        // Create a playlist file
        var playlistPath = Path.Combine(_tempDir, "My Videos.vidpl");
        var playlist = new Models.Playlist("Test Playlist");
        playlist.Items.Add(new Models.PlaylistItem(@"C:\Videos\video1.mp4"));
        playlist.Items.Add(new Models.PlaylistItem(@"C:\Videos\video2.mp4"));
        await _fileService.SaveAsync(playlist, playlistPath);

        await _vm.LoadPlaylistFromPathAsync(playlistPath);

        // Name derived from file name, not JSON content
        Assert.Equal("My Videos", _vm.PlaylistName);
        Assert.Equal(2, _vm.Items.Count);
        Assert.Null(_vm.CurrentItem);
    }

    // ── Dirty State Prompts ──

    [Fact]
    public void PromptSaveDirtyPlaylist_ReturnsTrue_WhenNotDirty()
    {
        _vm.CurrentPlaylist.IsDirty = false;

        var result = _vm.PromptSaveDirtyPlaylist();

        Assert.True(result);
        _dialogMock.Verify(d => d.ShowConfirmationDialog(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void PromptSaveDirtyPlaylist_ReturnsTrue_WhenUserChoosesNo()
    {
        _vm.AddItem(@"C:\Videos\video1.mp4"); // makes it dirty

        _dialogMock.Setup(d => d.ShowConfirmationDialog(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        var result = _vm.PromptSaveDirtyPlaylist();

        Assert.True(result); // No = discard and proceed
    }

    [Fact]
    public void PromptSaveDirtyPlaylist_ReturnsFalse_WhenUserChoosesCancel()
    {
        _vm.AddItem(@"C:\Videos\video1.mp4");

        _dialogMock.Setup(d => d.ShowConfirmationDialog(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((bool?)null);

        var result = _vm.PromptSaveDirtyPlaylist();

        Assert.False(result);
    }

    [Fact]
    public void PromptSaveDirtyPlaylist_SavesAndReturnsTrue_WhenUserChoosesYes()
    {
        var savePath = Path.Combine(_tempDir, "prompted.vidpl");
        _vm.AddItem(@"C:\Videos\video1.mp4");

        _dialogMock.Setup(d => d.ShowConfirmationDialog(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        _dialogMock.Setup(d => d.ShowSaveFileDialog(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(savePath);

        var result = _vm.PromptSaveDirtyPlaylist();

        Assert.True(result);
        Assert.True(File.Exists(savePath));
    }

    // ── NewPlaylistCommand with dirty check ──

    [Fact]
    public void NewPlaylistCommand_PromptsSaveWhenDirty()
    {
        _vm.AddItem(@"C:\Videos\video1.mp4");

        // User cancels — the new playlist should NOT be created
        _dialogMock.Setup(d => d.ShowConfirmationDialog(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((bool?)null);

        _vm.NewPlaylistCommand.Execute(null);

        // Items should still be present (new was cancelled)
        Assert.Single(_vm.Items);
    }

    // ── Status Bar Text ──

    [Fact]
    public void StatusText_ShowsItemCountWhenNotPlaying()
    {
        _vm.AddItem(@"C:\Videos\video1.mp4");
        _vm.AddItem(@"C:\Videos\video2.mp4");

        Assert.Contains("2 items", _vm.StatusText);
        Assert.Contains("Untitled Playlist", _vm.StatusText);
    }

    [Fact]
    public void StatusText_ShowsPlayingPosition()
    {
        var filePath = Path.Combine(_tempDir, "test.mp4");
        File.WriteAllText(filePath, "fake");

        _vm.AddItem(filePath);
        _vm.AddItem(@"C:\Videos\video2.mp4");

        // Simulate playing the first item
        _vm.CurrentItem = _vm.Items[0];
        _vm.UpdateStatusText();

        Assert.Contains("Playing 1 of 2", _vm.StatusText);
    }

    [Fact]
    public void StatusText_InvokesUpdateStatusBarCallback()
    {
        _statusBarUpdates.Clear();
        _vm.AddItem(@"C:\Videos\video1.mp4");

        Assert.NotEmpty(_statusBarUpdates);
    }

    // ── Recent Playlists ──

    [Fact]
    public void AddRecentPlaylist_AddsToTop()
    {
        _vm.AddRecentPlaylist(@"C:\Playlists\a.vidpl");
        _vm.AddRecentPlaylist(@"C:\Playlists\b.vidpl");

        Assert.Equal(2, _vm.RecentPlaylists.Count);
        Assert.Equal(@"C:\Playlists\b.vidpl", _vm.RecentPlaylists[0]);
        Assert.Equal(@"C:\Playlists\a.vidpl", _vm.RecentPlaylists[1]);
    }

    [Fact]
    public void AddRecentPlaylist_DeduplicatesCaseInsensitive()
    {
        _vm.AddRecentPlaylist(@"C:\Playlists\a.vidpl");
        _vm.AddRecentPlaylist(@"C:\Playlists\b.vidpl");
        _vm.AddRecentPlaylist(@"c:\playlists\A.VIDPL");

        Assert.Equal(2, _vm.RecentPlaylists.Count);
        Assert.Equal(@"c:\playlists\A.VIDPL", _vm.RecentPlaylists[0]);
    }

    [Fact]
    public void AddRecentPlaylist_LimitsToMax10()
    {
        for (var i = 0; i < 15; i++)
            _vm.AddRecentPlaylist($@"C:\Playlists\playlist{i}.vidpl");

        Assert.Equal(10, _vm.RecentPlaylists.Count);
    }

    [Fact]
    public void AddRecentPlaylist_PersistsToSettings()
    {
        _vm.AddRecentPlaylist(@"C:\Playlists\a.vidpl");

        _settingsMock.Verify(s => s.Set("recentPlaylists", It.Is<string>(v => v.Contains(@"C:\Playlists\a.vidpl"))), Times.Once);
    }

    [Fact]
    public async Task SavePlaylist_AddsToRecentPlaylists()
    {
        var savePath = Path.Combine(_tempDir, "recent.vidpl");
        _vm.AddItem(@"C:\Videos\video1.mp4");
        _dialogMock.Setup(d => d.ShowSaveFileDialog(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(savePath);

        await _vm.SaveCurrentPlaylistAsync(saveAs: false);

        Assert.Contains(savePath, _vm.RecentPlaylists);
    }

    [Fact]
    public async Task LoadPlaylist_AddsToRecentPlaylists()
    {
        var playlistPath = Path.Combine(_tempDir, "recent-load.vidpl");
        var playlist = new Models.Playlist("Recent Test");
        await _fileService.SaveAsync(playlist, playlistPath);

        await _vm.LoadPlaylistFromPathAsync(playlistPath);

        Assert.Contains(playlistPath, _vm.RecentPlaylists);
    }

    [Fact]
    public void RecentPlaylists_RemovesStaleEntries()
    {
        var stalePath = @"C:\NonExistent\stale_playlist_12345.vidpl";
        _vm.AddRecentPlaylist(stalePath);

        // Simulate opening a stale recent entry
        _vm.OpenRecentPlaylistCommand.Execute(stalePath);

        Assert.DoesNotContain(stalePath, _vm.RecentPlaylists);
    }

    // ── HandleFileDrop — unsupported files ──

    [Fact]
    public void HandleFileDrop_IgnoresUnsupportedFiles()
    {
        var txtFile = Path.Combine(_tempDir, "readme.txt");
        File.WriteAllText(txtFile, "fake");

        _vm.HandleFileDrop([txtFile]);

        Assert.Empty(_vm.Items);
    }

    [Fact]
    public void HandleFileDrop_MixedFiles_AddsOnlyVideoFiles()
    {
        var mp4File = Path.Combine(_tempDir, "video.mp4");
        var txtFile = Path.Combine(_tempDir, "readme.txt");
        File.WriteAllText(mp4File, "fake");
        File.WriteAllText(txtFile, "fake");

        _vm.HandleFileDrop([mp4File, txtFile]);

        Assert.Single(_vm.Items);
        Assert.Equal("video.mp4", _vm.Items[0].FileName);
    }

    [Fact]
    public void HandleFileDrop_FolderWithMixedFiles_AddsOnlyVideoFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "video.mkv"), "fake");
        File.WriteAllText(Path.Combine(_tempDir, "script.funscript"), "fake");
        File.WriteAllText(Path.Combine(_tempDir, "notes.txt"), "fake");

        _vm.HandleFileDrop([_tempDir]);

        Assert.Single(_vm.Items);
        Assert.Equal("video.mkv", _vm.Items[0].FileName);
    }

    // ── HandleFileDrop — .vidpl playlist files ──

    [Fact]
    public async Task HandleFileDrop_VidplFile_OpensPlaylist()
    {
        // Create a playlist file
        var playlistPath = Path.Combine(_tempDir, "My Playlist.vidpl");
        var playlist = new Models.Playlist("Test");
        playlist.Items.Add(new Models.PlaylistItem(@"C:\Videos\video1.mp4"));
        await _fileService.SaveAsync(playlist, playlistPath);

        _vm.HandleFileDrop([playlistPath]);

        // Allow async load to complete
        await Task.Delay(100);

        Assert.Equal("My Playlist", _vm.PlaylistName);
        Assert.Single(_vm.Items);
    }

    [Fact]
    public async Task HandleFileDrop_VidplFile_PromptsSaveWhenDirty()
    {
        // Make playlist dirty
        _vm.AddItem(@"C:\Videos\existing.mp4");

        // Create a playlist file to drop
        var playlistPath = Path.Combine(_tempDir, "New.vidpl");
        var playlist = new Models.Playlist("New");
        await _fileService.SaveAsync(playlist, playlistPath);

        // User cancels save prompt — drop should be cancelled
        _dialogMock.Setup(d => d.ShowConfirmationDialog(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((bool?)null);

        _vm.HandleFileDrop([playlistPath]);

        // Original items should remain
        Assert.Single(_vm.Items);
        Assert.Equal("Untitled Playlist", _vm.PlaylistName);
    }

    // ── Auto-Save (vp-007) ──

    [Fact]
    public async Task AutoSaveIfEnabled_SavesWhenPathExists()
    {
        var savePath = Path.Combine(_tempDir, "auto.vidpl");
        _vm.CurrentPlaylist.FilePath = savePath;
        _vm.AddItem(@"C:\Videos\video1.mp4");

        // Enable auto-save
        _settingsMock.Setup(s => s.Get("autoSave", false)).Returns(true);

        _vm.AutoSaveIfEnabled();

        // Allow async save to complete
        await Task.Delay(100);

        Assert.True(File.Exists(savePath));
    }

    [Fact]
    public void AutoSaveIfEnabled_PromptsDialogWhenNoPath()
    {
        _vm.AddItem(@"C:\Videos\video1.mp4");

        // Enable auto-save
        _settingsMock.Setup(s => s.Get("autoSave", false)).Returns(true);

        // Dialog cancelled — should not crash
        _dialogMock.Setup(d => d.ShowSaveFileDialog(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string?)null);

        _vm.AutoSaveIfEnabled();

        _dialogMock.Verify(d => d.ShowSaveFileDialog(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void AutoSaveIfEnabled_DoesNothingWhenDisabled()
    {
        _vm.AddItem(@"C:\Videos\video1.mp4");

        // Auto-save disabled (default)
        _settingsMock.Setup(s => s.Get("autoSave", false)).Returns(false);

        _vm.AutoSaveIfEnabled();

        _dialogMock.Verify(d => d.ShowSaveFileDialog(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void AddFromFileNode_TriggersAutoSave()
    {
        var savePath = Path.Combine(_tempDir, "autosave.vidpl");
        _vm.CurrentPlaylist.FilePath = savePath;

        // Enable auto-save
        _settingsMock.Setup(s => s.Get("autoSave", false)).Returns(true);

        var filePath = Path.Combine(_tempDir, "video.mp4");
        File.WriteAllText(filePath, "fake");

        _vm.AddFromFileNode(filePath, isDirectory: false);

        // File should be saved (auto-save triggered)
        Assert.True(File.Exists(savePath));
    }

    // ── RemoveItem ──

    [Fact]
    public void RemoveItem_RemovesItemFromPlaylist()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItem(@"C:\Videos\b.mp4");
        _vm.AddItem(@"C:\Videos\c.mp4");

        var itemB = _vm.Items[1];
        _vm.RemoveItem(itemB);

        Assert.Equal(2, _vm.Items.Count);
        Assert.Equal("a.mp4", _vm.Items[0].FileName);
        Assert.Equal("c.mp4", _vm.Items[1].FileName);
    }

    [Fact]
    public void RemoveItem_ClearsCurrentItemWhenRemoved()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItem(@"C:\Videos\b.mp4");

        var itemA = _vm.Items[0];
        _vm.CurrentItem = itemA;

        _vm.RemoveItem(itemA);

        Assert.Null(_vm.CurrentItem);
        Assert.Single(_vm.Items);
    }

    [Fact]
    public void RemoveItem_DoesNotClearCurrentItemWhenOtherRemoved()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItem(@"C:\Videos\b.mp4");

        var itemA = _vm.Items[0];
        var itemB = _vm.Items[1];
        _vm.CurrentItem = itemA;

        _vm.RemoveItem(itemB);

        Assert.Same(itemA, _vm.CurrentItem);
        Assert.Single(_vm.Items);
    }

    [Fact]
    public void RemoveItem_RemovesLastItem_HasItemsBecomesFalse()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        Assert.True(_vm.HasItems);

        _vm.RemoveItem(_vm.Items[0]);

        Assert.False(_vm.HasItems);
        Assert.Empty(_vm.Items);
    }

    [Fact]
    public void RemoveItem_NullItem_DoesNothing()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");

        _vm.RemoveItem(null);

        Assert.Single(_vm.Items);
    }

    [Fact]
    public void RemoveItem_SetsDirtyFlag()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.CurrentPlaylist.IsDirty = false;

        _vm.RemoveItem(_vm.Items[0]);

        Assert.True(_vm.CurrentPlaylist.IsDirty);
    }

    [Fact]
    public void RemoveItem_TriggersAutoSave()
    {
        var savePath = Path.Combine(_tempDir, "autosave.vidpl");
        _vm.CurrentPlaylist.FilePath = savePath;
        _settingsMock.Setup(s => s.Get("autoSave", false)).Returns(true);

        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItem(@"C:\Videos\b.mp4");

        _vm.RemoveItem(_vm.Items[0]);

        Assert.True(File.Exists(savePath));
    }

    // ── RemoveItemCommand ──

    [Fact]
    public void RemoveItemCommand_RemovesSelectedItem()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItem(@"C:\Videos\b.mp4");

        var itemA = _vm.Items[0];
        _vm.RemoveItemCommand.Execute(itemA);

        Assert.Single(_vm.Items);
        Assert.Equal("b.mp4", _vm.Items[0].FileName);
    }

    // ── MoveItem ──

    [Fact]
    public void MoveItem_MovesItemForward()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItem(@"C:\Videos\b.mp4");
        _vm.AddItem(@"C:\Videos\c.mp4");

        _vm.MoveItem(0, 2);

        Assert.Equal("b.mp4", _vm.Items[0].FileName);
        Assert.Equal("c.mp4", _vm.Items[1].FileName);
        Assert.Equal("a.mp4", _vm.Items[2].FileName);
    }

    [Fact]
    public void MoveItem_MovesItemBackward()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItem(@"C:\Videos\b.mp4");
        _vm.AddItem(@"C:\Videos\c.mp4");

        _vm.MoveItem(2, 0);

        Assert.Equal("c.mp4", _vm.Items[0].FileName);
        Assert.Equal("a.mp4", _vm.Items[1].FileName);
        Assert.Equal("b.mp4", _vm.Items[2].FileName);
    }

    [Fact]
    public void MoveItem_SameIndex_DoesNothing()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItem(@"C:\Videos\b.mp4");
        _vm.CurrentPlaylist.IsDirty = false;

        _vm.MoveItem(0, 0);

        Assert.Equal("a.mp4", _vm.Items[0].FileName);
        Assert.Equal("b.mp4", _vm.Items[1].FileName);
        Assert.False(_vm.CurrentPlaylist.IsDirty);
    }

    [Fact]
    public void MoveItem_InvalidFromIndex_DoesNothing()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItem(@"C:\Videos\b.mp4");

        _vm.MoveItem(-1, 0);
        _vm.MoveItem(5, 0);

        Assert.Equal("a.mp4", _vm.Items[0].FileName);
        Assert.Equal("b.mp4", _vm.Items[1].FileName);
    }

    [Fact]
    public void MoveItem_InvalidToIndex_DoesNothing()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItem(@"C:\Videos\b.mp4");

        _vm.MoveItem(0, -1);
        _vm.MoveItem(0, 5);

        Assert.Equal("a.mp4", _vm.Items[0].FileName);
        Assert.Equal("b.mp4", _vm.Items[1].FileName);
    }

    [Fact]
    public void MoveItem_SetsDirtyFlag()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItem(@"C:\Videos\b.mp4");
        _vm.CurrentPlaylist.IsDirty = false;

        _vm.MoveItem(0, 1);

        Assert.True(_vm.CurrentPlaylist.IsDirty);
    }

    [Fact]
    public void MoveItem_TriggersAutoSave()
    {
        var savePath = Path.Combine(_tempDir, "autosave.vidpl");
        _vm.CurrentPlaylist.FilePath = savePath;
        _settingsMock.Setup(s => s.Get("autoSave", false)).Returns(true);

        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItem(@"C:\Videos\b.mp4");

        _vm.MoveItem(0, 1);

        Assert.True(File.Exists(savePath));
    }

    // ── MoveUpCommand ──

    [Fact]
    public void MoveUpCommand_MovesItemUp()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItem(@"C:\Videos\b.mp4");
        _vm.AddItem(@"C:\Videos\c.mp4");

        _vm.MoveUpCommand.Execute(_vm.Items[1]);

        Assert.Equal("b.mp4", _vm.Items[0].FileName);
        Assert.Equal("a.mp4", _vm.Items[1].FileName);
        Assert.Equal("c.mp4", _vm.Items[2].FileName);
    }

    [Fact]
    public void MoveUpCommand_FirstItem_DoesNothing()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItem(@"C:\Videos\b.mp4");

        _vm.MoveUpCommand.Execute(_vm.Items[0]);

        Assert.Equal("a.mp4", _vm.Items[0].FileName);
        Assert.Equal("b.mp4", _vm.Items[1].FileName);
    }

    // ── MoveDownCommand ──

    [Fact]
    public void MoveDownCommand_MovesItemDown()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItem(@"C:\Videos\b.mp4");
        _vm.AddItem(@"C:\Videos\c.mp4");

        _vm.MoveDownCommand.Execute(_vm.Items[1]);

        Assert.Equal("a.mp4", _vm.Items[0].FileName);
        Assert.Equal("c.mp4", _vm.Items[1].FileName);
        Assert.Equal("b.mp4", _vm.Items[2].FileName);
    }

    [Fact]
    public void MoveDownCommand_LastItem_DoesNothing()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItem(@"C:\Videos\b.mp4");

        _vm.MoveDownCommand.Execute(_vm.Items[1]);

        Assert.Equal("a.mp4", _vm.Items[0].FileName);
        Assert.Equal("b.mp4", _vm.Items[1].FileName);
    }

    // ── MoveToTopCommand ──

    [Fact]
    public void MoveToTopCommand_MovesItemToTop()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItem(@"C:\Videos\b.mp4");
        _vm.AddItem(@"C:\Videos\c.mp4");

        _vm.MoveToTopCommand.Execute(_vm.Items[2]);

        Assert.Equal("c.mp4", _vm.Items[0].FileName);
        Assert.Equal("a.mp4", _vm.Items[1].FileName);
        Assert.Equal("b.mp4", _vm.Items[2].FileName);
    }

    [Fact]
    public void MoveToTopCommand_AlreadyAtTop_DoesNothing()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItem(@"C:\Videos\b.mp4");
        _vm.CurrentPlaylist.IsDirty = false;

        _vm.MoveToTopCommand.Execute(_vm.Items[0]);

        Assert.Equal("a.mp4", _vm.Items[0].FileName);
        Assert.False(_vm.CurrentPlaylist.IsDirty);
    }

    // ── MoveToBottomCommand ──

    [Fact]
    public void MoveToBottomCommand_MovesItemToBottom()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItem(@"C:\Videos\b.mp4");
        _vm.AddItem(@"C:\Videos\c.mp4");

        _vm.MoveToBottomCommand.Execute(_vm.Items[0]);

        Assert.Equal("b.mp4", _vm.Items[0].FileName);
        Assert.Equal("c.mp4", _vm.Items[1].FileName);
        Assert.Equal("a.mp4", _vm.Items[2].FileName);
    }

    [Fact]
    public void MoveToBottomCommand_AlreadyAtBottom_DoesNothing()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItem(@"C:\Videos\b.mp4");
        _vm.CurrentPlaylist.IsDirty = false;

        _vm.MoveToBottomCommand.Execute(_vm.Items[1]);

        Assert.Equal("b.mp4", _vm.Items[1].FileName);
        Assert.False(_vm.CurrentPlaylist.IsDirty);
    }
}
