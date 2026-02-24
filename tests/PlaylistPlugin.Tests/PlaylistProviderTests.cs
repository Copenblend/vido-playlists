using PlaylistPlugin.Models;
using PlaylistPlugin.Services;
using System.Collections.ObjectModel;
using Xunit;

namespace PlaylistPlugin.Tests;

public class PlaylistProviderTests
{
    private readonly PlaylistProvider _provider;
    private readonly ObservableCollection<PlaylistItem> _items;

    public PlaylistProviderTests()
    {
        _provider = new PlaylistProvider();
        _items =
        [
            new PlaylistItem(@"C:\Videos\a.mp4"),
            new PlaylistItem(@"C:\Videos\b.mp4"),
            new PlaylistItem(@"C:\Videos\c.mp4"),
        ];
    }

    // ── IsActive ──

    [Fact]
    public void IsActive_FalseByDefault()
    {
        Assert.False(_provider.IsActive);
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        _provider.Activate(_items, 0);

        Assert.True(_provider.IsActive);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        _provider.Activate(_items, 0);
        _provider.Deactivate();

        Assert.False(_provider.IsActive);
    }

    [Fact]
    public void Activate_SetsCurrentIndex()
    {
        _provider.Activate(_items, 1);

        Assert.Equal(1, _provider.CurrentIndex);
    }

    [Fact]
    public void Deactivate_ResetsCurrentIndex()
    {
        _provider.Activate(_items, 2);
        _provider.Deactivate();

        Assert.Equal(-1, _provider.CurrentIndex);
    }

    [Fact]
    public void SetCurrentIndex_UpdatesIndex()
    {
        _provider.Activate(_items, 0);
        _provider.SetCurrentIndex(2);

        Assert.Equal(2, _provider.CurrentIndex);
    }

    // ── GetNextFile ──

    [Fact]
    public void GetNextFile_ReturnsNextItem()
    {
        _provider.Activate(_items, 0);

        var next = _provider.GetNextFile();

        Assert.Equal(@"C:\Videos\b.mp4", next);
        Assert.Equal(1, _provider.CurrentIndex);
    }

    [Fact]
    public void GetNextFile_WrapsFromLastToFirst()
    {
        _provider.Activate(_items, 2);

        var next = _provider.GetNextFile();

        Assert.Equal(@"C:\Videos\a.mp4", next);
        Assert.Equal(0, _provider.CurrentIndex);
    }

    [Fact]
    public void GetNextFile_FromMiddle()
    {
        _provider.Activate(_items, 1);

        var next = _provider.GetNextFile();

        Assert.Equal(@"C:\Videos\c.mp4", next);
        Assert.Equal(2, _provider.CurrentIndex);
    }

    [Fact]
    public void GetNextFile_ReturnsNullWhenNotActive()
    {
        Assert.Null(_provider.GetNextFile());
    }

    [Fact]
    public void GetNextFile_ReturnsNullWhenEmpty()
    {
        _provider.Activate(new ObservableCollection<PlaylistItem>(), 0);

        Assert.Null(_provider.GetNextFile());
    }

    // ── GetPreviousFile ──

    [Fact]
    public void GetPreviousFile_ReturnsPreviousItem()
    {
        _provider.Activate(_items, 2);

        var prev = _provider.GetPreviousFile();

        Assert.Equal(@"C:\Videos\b.mp4", prev);
        Assert.Equal(1, _provider.CurrentIndex);
    }

    [Fact]
    public void GetPreviousFile_WrapsFromFirstToLast()
    {
        _provider.Activate(_items, 0);

        var prev = _provider.GetPreviousFile();

        Assert.Equal(@"C:\Videos\c.mp4", prev);
        Assert.Equal(2, _provider.CurrentIndex);
    }

    [Fact]
    public void GetPreviousFile_FromMiddle()
    {
        _provider.Activate(_items, 1);

        var prev = _provider.GetPreviousFile();

        Assert.Equal(@"C:\Videos\a.mp4", prev);
        Assert.Equal(0, _provider.CurrentIndex);
    }

    [Fact]
    public void GetPreviousFile_ReturnsNullWhenNotActive()
    {
        Assert.Null(_provider.GetPreviousFile());
    }

    // ── Non-video files skipped ──

    [Fact]
    public void GetNextFile_SkipsNonVideoFiles()
    {
        var mixedItems = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Videos\a.mp4"),
            new(@"C:\Videos\a.funscript"),
            new(@"C:\Videos\b.mp4"),
        };

        _provider.Activate(mixedItems, 0);

        var next = _provider.GetNextFile();

        Assert.Equal(@"C:\Videos\b.mp4", next);
        Assert.Equal(2, _provider.CurrentIndex);
    }

    [Fact]
    public void GetPreviousFile_SkipsNonVideoFiles()
    {
        var mixedItems = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Videos\a.mp4"),
            new(@"C:\Videos\a.funscript"),
            new(@"C:\Videos\b.mp4"),
        };

        _provider.Activate(mixedItems, 2);

        var prev = _provider.GetPreviousFile();

        Assert.Equal(@"C:\Videos\a.mp4", prev);
        Assert.Equal(0, _provider.CurrentIndex);
    }

    [Fact]
    public void GetNextFile_ReturnsNullWhenNoVideoFiles()
    {
        var noVideoItems = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Files\script.funscript"),
            new(@"C:\Files\data.json"),
        };

        _provider.Activate(noVideoItems, 0);

        Assert.Null(_provider.GetNextFile());
    }

    // ── Single video playlist ──

    [Fact]
    public void GetNextFile_SingleVideoPlaylist_ReturnsSame()
    {
        var single = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Videos\only.mp4"),
        };

        _provider.Activate(single, 0);

        var next = _provider.GetNextFile();

        // Wraps to itself
        Assert.Equal(@"C:\Videos\only.mp4", next);
        Assert.Equal(0, _provider.CurrentIndex);
    }

    [Fact]
    public void GetPreviousFile_SingleVideoPlaylist_ReturnsSame()
    {
        var single = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Videos\only.mp4"),
        };

        _provider.Activate(single, 0);

        var prev = _provider.GetPreviousFile();

        Assert.Equal(@"C:\Videos\only.mp4", prev);
        Assert.Equal(0, _provider.CurrentIndex);
    }

    // ── Sequential navigation ──

    [Fact]
    public void GetNextFile_MultipleCallsTraverseFullPlaylist()
    {
        _provider.Activate(_items, 0);

        Assert.Equal(@"C:\Videos\b.mp4", _provider.GetNextFile());
        Assert.Equal(@"C:\Videos\c.mp4", _provider.GetNextFile());
        Assert.Equal(@"C:\Videos\a.mp4", _provider.GetNextFile()); // wrap
        Assert.Equal(@"C:\Videos\b.mp4", _provider.GetNextFile()); // loop again
    }

    [Fact]
    public void GetPreviousFile_MultipleCallsTraverseFullPlaylist()
    {
        _provider.Activate(_items, 2);

        Assert.Equal(@"C:\Videos\b.mp4", _provider.GetPreviousFile());
        Assert.Equal(@"C:\Videos\a.mp4", _provider.GetPreviousFile());
        Assert.Equal(@"C:\Videos\c.mp4", _provider.GetPreviousFile()); // wrap
    }

    // ── Activate with null throws ──

    [Fact]
    public void Activate_ThrowsOnNullItems()
    {
        Assert.Throws<ArgumentNullException>(() => _provider.Activate(null!, 0));
    }
}
