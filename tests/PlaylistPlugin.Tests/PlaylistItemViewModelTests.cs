using System.IO;
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
    public void FileExists_RemainsCachedUntilRefresh()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"playlist-item-{Guid.NewGuid():N}.mp4");
        var item = new PlaylistItem(tempPath);
        var vm = new PlaylistItemViewModel(item);

        Assert.False(vm.FileExists);

        File.WriteAllText(tempPath, "test");
        try
        {
            Assert.False(vm.FileExists);

            vm.RefreshFileExists();

            Assert.True(vm.FileExists);
            Assert.Equal(tempPath, vm.ToolTipText);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public void RefreshFileExists_RaisesPropertyChangedForFileExistsAndToolTipText()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"playlist-item-{Guid.NewGuid():N}.mp4");
        var item = new PlaylistItem(tempPath);
        var vm = new PlaylistItemViewModel(item);
        var changedProperties = new List<string>();

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
                changedProperties.Add(e.PropertyName);
        };

        File.WriteAllText(tempPath, "test");
        try
        {
            vm.RefreshFileExists();

            Assert.Contains(nameof(PlaylistItemViewModel.FileExists), changedProperties);
            Assert.Contains(nameof(PlaylistItemViewModel.ToolTipText), changedProperties);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public void Model_ExposesUnderlyingPlaylistItem()
    {
        var item = new PlaylistItem(@"C:\Videos\test.mp4");
        var vm = new PlaylistItemViewModel(item);

        Assert.Same(item, vm.Model);
    }
}
