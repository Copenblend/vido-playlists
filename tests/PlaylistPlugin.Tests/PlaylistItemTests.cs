using PlaylistPlugin.Models;
using Xunit;

namespace PlaylistPlugin.Tests;

public class PlaylistItemTests
{
    [Fact]
    public void Constructor_SetsFilePathAndFileName()
    {
        var item = new PlaylistItem(@"C:\Videos\my video.mp4");

        Assert.Equal(@"C:\Videos\my video.mp4", item.FilePath);
        Assert.Equal("my video.mp4", item.FileName);
    }

    [Fact]
    public void Constructor_ThrowsOnNullOrWhiteSpace()
    {
        Assert.ThrowsAny<ArgumentException>(() => new PlaylistItem(null!));
        Assert.ThrowsAny<ArgumentException>(() => new PlaylistItem(""));
        Assert.ThrowsAny<ArgumentException>(() => new PlaylistItem("   "));
    }

    [Fact]
    public void Equals_SamePathCaseInsensitive_ReturnsTrue()
    {
        var a = new PlaylistItem(@"C:\Videos\video.mp4");
        var b = new PlaylistItem(@"c:\VIDEOS\VIDEO.MP4");

        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentPath_ReturnsFalse()
    {
        var a = new PlaylistItem(@"C:\Videos\video1.mp4");
        var b = new PlaylistItem(@"C:\Videos\video2.mp4");

        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var item = new PlaylistItem(@"C:\Videos\video.mp4");
        Assert.False(item.Equals(null));
    }

    [Fact]
    public void ToString_ReturnsFileName()
    {
        var item = new PlaylistItem(@"C:\Videos\my video.mp4");
        Assert.Equal("my video.mp4", item.ToString());
    }

    [Fact]
    public void Equals_ObjectOverload_Works()
    {
        var a = new PlaylistItem(@"C:\Videos\video.mp4");
        object b = new PlaylistItem(@"C:\Videos\video.mp4");

        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equals_DifferentType_ReturnsFalse()
    {
        var item = new PlaylistItem(@"C:\Videos\video.mp4");
        Assert.False(item.Equals("not a playlist item"));
    }
}
