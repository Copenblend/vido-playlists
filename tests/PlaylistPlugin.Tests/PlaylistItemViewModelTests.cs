using PlaylistPlugin.Models;
using PlaylistPlugin.ViewModels;
using Xunit;

namespace PlaylistPlugin.Tests;

public class PlaylistItemViewModelTests
{
    [Fact]
    public void Constructor_SetsPropertiesFromModel()
    {
        var item = new PlaylistItem(@"C:\Videos\test.mp4");
        var vm = new PlaylistItemViewModel(item);

        Assert.Equal("test.mp4", vm.FileName);
        Assert.Equal(@"C:\Videos\test.mp4", vm.FilePath);
        Assert.False(vm.IsPlaying);
    }

    [Fact]
    public void Constructor_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => new PlaylistItemViewModel(null!));
    }

    [Fact]
    public void IsPlaying_RaisesPropertyChanged()
    {
        var item = new PlaylistItem(@"C:\Videos\test.mp4");
        var vm = new PlaylistItemViewModel(item);
        var raised = false;

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlaylistItemViewModel.IsPlaying))
                raised = true;
        };

        vm.IsPlaying = true;

        Assert.True(raised);
        Assert.True(vm.IsPlaying);
    }

    [Fact]
    public void IsPlaying_SameValue_DoesNotRaisePropertyChanged()
    {
        var item = new PlaylistItem(@"C:\Videos\test.mp4");
        var vm = new PlaylistItemViewModel(item);
        var raised = false;

        vm.PropertyChanged += (_, _) => raised = true;

        vm.IsPlaying = false; // already false

        Assert.False(raised);
    }

    [Fact]
    public void FileExists_ReturnsFalseForNonexistentFile()
    {
        var item = new PlaylistItem(@"C:\NonExistent\does_not_exist_12345.mp4");
        var vm = new PlaylistItemViewModel(item);

        Assert.False(vm.FileExists);
    }

    [Fact]
    public void ToolTipText_IncludesNotFoundForMissingFile()
    {
        var item = new PlaylistItem(@"C:\NonExistent\does_not_exist_12345.mp4");
        var vm = new PlaylistItemViewModel(item);

        Assert.Contains("file not found", vm.ToolTipText);
    }

    [Fact]
    public void Model_ExposesUnderlyingPlaylistItem()
    {
        var item = new PlaylistItem(@"C:\Videos\test.mp4");
        var vm = new PlaylistItemViewModel(item);

        Assert.Same(item, vm.Model);
    }
}
