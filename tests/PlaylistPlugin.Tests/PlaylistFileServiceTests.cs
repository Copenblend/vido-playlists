using System.IO;
using PlaylistPlugin.Models;
using PlaylistPlugin.Services;
using Xunit;

namespace PlaylistPlugin.Tests;

public class PlaylistFileServiceTests : IDisposable
{
    private readonly PlaylistFileService _service = new();
    private readonly string _tempDir;

    public PlaylistFileServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "playlist_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ── CreateNew ──

    [Fact]
    public void CreateNew_ReturnsPlaylistWithDefaultName()
    {
        var playlist = _service.CreateNew();

        Assert.Equal("Untitled Playlist", playlist.Name);
        Assert.Empty(playlist.Items);
        Assert.Null(playlist.FilePath);
        Assert.False(playlist.IsDirty);
    }

    // ── SaveAsync / LoadAsync round-trip ──

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesData()
    {
        var original = new Playlist("My Playlist",
        [
            new PlaylistItem(@"C:\Videos\video1.mp4"),
            new PlaylistItem(@"C:\Videos\video1.funscript"),
            new PlaylistItem(@"C:\Videos\video2.mkv")
        ]);

        var path = Path.Combine(_tempDir, "test.vidpl");
        await _service.SaveAsync(original, path);
        var loaded = await _service.LoadAsync(path);

        Assert.Equal("My Playlist", loaded.Name);
        Assert.Equal(3, loaded.Items.Count);
        Assert.Equal(@"C:\Videos\video1.mp4", loaded.Items[0].FilePath);
        Assert.Equal(@"C:\Videos\video1.funscript", loaded.Items[1].FilePath);
        Assert.Equal(@"C:\Videos\video2.mkv", loaded.Items[2].FilePath);
        Assert.Equal(path, loaded.FilePath);
        Assert.False(loaded.IsDirty);
    }

    [Fact]
    public async Task SaveAsync_SetsFilePathAndClearsDirty()
    {
        var playlist = new Playlist("Test");
        playlist.Items.Add(new PlaylistItem(@"C:\a.mp4"));
        Assert.True(playlist.IsDirty);

        var path = Path.Combine(_tempDir, "save_test.vidpl");
        await _service.SaveAsync(playlist, path);

        Assert.Equal(path, playlist.FilePath);
        Assert.False(playlist.IsDirty);
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectoryIfMissing()
    {
        var playlist = new Playlist("Test");
        var path = Path.Combine(_tempDir, "subdir", "nested", "test.vidpl");

        await _service.SaveAsync(playlist, path);

        Assert.True(File.Exists(path));
    }

    // ── Empty playlist ──

    [Fact]
    public async Task SaveAndLoad_EmptyPlaylist_Works()
    {
        var original = new Playlist("Empty");
        var path = Path.Combine(_tempDir, "empty.vidpl");

        await _service.SaveAsync(original, path);
        var loaded = await _service.LoadAsync(path);

        Assert.Equal("Empty", loaded.Name);
        Assert.Empty(loaded.Items);
        Assert.False(loaded.IsDirty);
    }

    // ── File format ──

    [Fact]
    public async Task SaveAsync_WritesValidJson()
    {
        var playlist = new Playlist("JSON Test",
        [
            new PlaylistItem(@"C:\Videos\test.mp4")
        ]);
        var path = Path.Combine(_tempDir, "json.vidpl");

        await _service.SaveAsync(playlist, path);

        var json = await File.ReadAllTextAsync(path);
        Assert.Contains("\"name\": \"JSON Test\"", json);
        Assert.Contains("\"filePath\": \"C:\\\\Videos\\\\test.mp4\"", json);
    }

    // ── Missing files kept in list ──

    [Fact]
    public async Task LoadAsync_KeepsItemsWithMissingFiles()
    {
        var playlist = new Playlist("Missing Files",
        [
            new PlaylistItem(@"C:\NonExistent\video.mp4"),
            new PlaylistItem(@"C:\NonExistent\companion.funscript")
        ]);

        var path = Path.Combine(_tempDir, "missing.vidpl");
        await _service.SaveAsync(playlist, path);
        var loaded = await _service.LoadAsync(path);

        // Items should be kept even though the files don't exist
        Assert.Equal(2, loaded.Items.Count);
        Assert.Equal(@"C:\NonExistent\video.mp4", loaded.Items[0].FilePath);
    }

    // ── Error handling ──

    [Fact]
    public async Task LoadAsync_ThrowsFileNotFound_WhenFileMissing()
    {
        var path = Path.Combine(_tempDir, "nonexistent.vidpl");

        await Assert.ThrowsAsync<FileNotFoundException>(() => _service.LoadAsync(path));
    }

    [Fact]
    public async Task LoadAsync_ThrowsInvalidData_OnCorruptJson()
    {
        var path = Path.Combine(_tempDir, "corrupt.vidpl");
        await File.WriteAllTextAsync(path, "not valid json {{{");

        await Assert.ThrowsAsync<InvalidDataException>(() => _service.LoadAsync(path));
    }

    [Fact]
    public async Task LoadAsync_ThrowsInvalidData_OnNullJson()
    {
        var path = Path.Combine(_tempDir, "null.vidpl");
        await File.WriteAllTextAsync(path, "null");

        await Assert.ThrowsAsync<InvalidDataException>(() => _service.LoadAsync(path));
    }

    [Fact]
    public async Task LoadAsync_HandlesEmptyName_UsesDefault()
    {
        var path = Path.Combine(_tempDir, "noname.vidpl");
        await File.WriteAllTextAsync(path, """{ "name": "", "items": [] }""");

        var loaded = await _service.LoadAsync(path);

        Assert.Equal("Untitled Playlist", loaded.Name);
    }

    [Fact]
    public async Task LoadAsync_SkipsItemsWithNullOrEmptyPaths()
    {
        var path = Path.Combine(_tempDir, "blanks.vidpl");
        await File.WriteAllTextAsync(path, """
        {
            "name": "Test",
            "items": [
                { "filePath": "C:\\Videos\\valid.mp4" },
                { "filePath": "" },
                { "filePath": null },
                { "filePath": "   " },
                { "filePath": "C:\\Videos\\also-valid.mkv" }
            ]
        }
        """);

        var loaded = await _service.LoadAsync(path);

        Assert.Equal(2, loaded.Items.Count);
        Assert.Equal(@"C:\Videos\valid.mp4", loaded.Items[0].FilePath);
        Assert.Equal(@"C:\Videos\also-valid.mkv", loaded.Items[1].FilePath);
    }

    [Fact]
    public async Task LoadAsync_HandlesNullItems_AsEmpty()
    {
        var path = Path.Combine(_tempDir, "nullitems.vidpl");
        await File.WriteAllTextAsync(path, """{ "name": "Test" }""");

        var loaded = await _service.LoadAsync(path);

        Assert.Equal("Test", loaded.Name);
        Assert.Empty(loaded.Items);
    }

    // ── Validation ──

    [Fact]
    public async Task SaveAsync_ThrowsOnNullPlaylist()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.SaveAsync(null!, Path.Combine(_tempDir, "test.vidpl")));
    }

    [Fact]
    public async Task SaveAsync_ThrowsOnNullOrWhiteSpacePath()
    {
        var playlist = new Playlist("Test");
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.SaveAsync(playlist, null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.SaveAsync(playlist, ""));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.SaveAsync(playlist, "   "));
    }

    [Fact]
    public async Task LoadAsync_ThrowsOnNullOrWhiteSpacePath()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.LoadAsync(null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.LoadAsync(""));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.LoadAsync("   "));
    }
}
