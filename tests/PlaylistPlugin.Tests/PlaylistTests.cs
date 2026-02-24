using PlaylistPlugin.Models;
using Xunit;

namespace PlaylistPlugin.Tests;

public class PlaylistTests
{
    [Fact]
    public void Constructor_SetsNameAndEmptyItems()
    {
        var playlist = new Playlist("Test Playlist");

        Assert.Equal("Test Playlist", playlist.Name);
        Assert.Empty(playlist.Items);
        Assert.Null(playlist.FilePath);
        Assert.False(playlist.IsDirty);
    }

    [Fact]
    public void Constructor_WithItems_PopulatesCollection()
    {
        var items = new[]
        {
            new PlaylistItem(@"C:\Videos\a.mp4"),
            new PlaylistItem(@"C:\Videos\b.mp4")
        };

        var playlist = new Playlist("Test", items);

        Assert.Equal(2, playlist.Items.Count);
        Assert.Equal(@"C:\Videos\a.mp4", playlist.Items[0].FilePath);
        Assert.Equal(@"C:\Videos\b.mp4", playlist.Items[1].FilePath);
    }

    [Fact]
    public void Constructor_ThrowsOnNullOrWhiteSpaceName()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Playlist(null!));
        Assert.ThrowsAny<ArgumentException>(() => new Playlist(""));
        Assert.ThrowsAny<ArgumentException>(() => new Playlist("   "));
    }

    [Fact]
    public void SettingName_MarksDirty()
    {
        var playlist = new Playlist("Original");
        playlist.IsDirty = false;

        playlist.Name = "Updated";

        Assert.True(playlist.IsDirty);
        Assert.Equal("Updated", playlist.Name);
    }

    [Fact]
    public void SettingName_SameValue_DoesNotMarkDirty()
    {
        var playlist = new Playlist("Same");
        playlist.IsDirty = false;

        playlist.Name = "Same";

        Assert.False(playlist.IsDirty);
    }

    [Fact]
    public void AddingItem_MarksDirty()
    {
        var playlist = new Playlist("Test");
        playlist.IsDirty = false;

        playlist.Items.Add(new PlaylistItem(@"C:\Videos\video.mp4"));

        Assert.True(playlist.IsDirty);
    }

    [Fact]
    public void RemovingItem_MarksDirty()
    {
        var item = new PlaylistItem(@"C:\Videos\video.mp4");
        var playlist = new Playlist("Test", [item]);
        playlist.IsDirty = false;

        playlist.Items.Remove(item);

        Assert.True(playlist.IsDirty);
    }

    [Fact]
    public void PropertyChanged_RaisedForName()
    {
        var playlist = new Playlist("Old");
        string? changedProperty = null;
        playlist.PropertyChanged += (_, e) => changedProperty = e.PropertyName;

        playlist.Name = "New";

        Assert.Equal("Name", changedProperty);
    }

    [Fact]
    public void PropertyChanged_RaisedForIsDirty()
    {
        var playlist = new Playlist("Test");
        playlist.IsDirty = false;
        var changedProperties = new List<string>();
        playlist.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        playlist.IsDirty = true;

        Assert.Contains("IsDirty", changedProperties);
    }

    [Fact]
    public void PropertyChanged_RaisedForFilePath()
    {
        var playlist = new Playlist("Test");
        string? changedProperty = null;
        playlist.PropertyChanged += (_, e) => changedProperty = e.PropertyName;

        playlist.FilePath = @"C:\Playlists\test.vidpl";

        Assert.Equal("FilePath", changedProperty);
    }

    [Fact]
    public void ClearingItems_MarksDirty()
    {
        var playlist = new Playlist("Test", [new PlaylistItem(@"C:\a.mp4")]);
        playlist.IsDirty = false;

        playlist.Items.Clear();

        Assert.True(playlist.IsDirty);
        Assert.Empty(playlist.Items);
    }
}
