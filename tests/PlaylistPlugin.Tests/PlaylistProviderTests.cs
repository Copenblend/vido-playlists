using PlaylistPlugin.Models;
using PlaylistPlugin.Services;
using PlaylistPlugin.ViewModels;
using System.Collections.ObjectModel;
using Vido.Core.Plugin;
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

    // ── Shuffle: Enable / Disable ──

    [Fact]
    public void IsShuffling_FalseByDefault()
    {
        Assert.False(_provider.IsShuffling);
    }

    [Fact]
    public void EnableShuffle_SetsIsShufflingTrue()
    {
        _provider.Activate(_items, 0);
        _provider.EnableShuffle();

        Assert.True(_provider.IsShuffling);
    }

    [Fact]
    public void DisableShuffle_SetsIsShufflingFalse()
    {
        _provider.Activate(_items, 0);
        _provider.EnableShuffle();
        _provider.DisableShuffle();

        Assert.False(_provider.IsShuffling);
    }

    [Fact]
    public void EnableShuffle_BuildsShuffledOrder()
    {
        _provider.Activate(_items, 0);
        _provider.EnableShuffle();

        Assert.NotNull(_provider.ShuffledIndices);
        Assert.Equal(_items.Count, _provider.ShuffledIndices!.Count);
    }

    [Fact]
    public void EnableShuffle_CurrentItemAtPositionZero()
    {
        _provider.Activate(_items, 1);
        _provider.EnableShuffle();

        Assert.Equal(1, _provider.ShuffledIndices![0]);
        Assert.Equal(0, _provider.ShufflePosition);
    }

    // ── Shuffle: Valid permutation ──

    [Fact]
    public void EnableShuffle_ProducesValidPermutation()
    {
        var items = new ObservableCollection<PlaylistItem>();
        for (var i = 0; i < 10; i++)
            items.Add(new PlaylistItem($@"C:\Videos\v{i}.mp4"));

        // Use seeded random for reproducibility
        var provider = new PlaylistProvider(new Random(42));
        provider.Activate(items, 3);
        provider.EnableShuffle();

        var indices = provider.ShuffledIndices!;

        // All indices present (no missing, no duplicates)
        Assert.Equal(10, indices.Count);
        var sorted = indices.OrderBy(x => x).ToList();
        for (var i = 0; i < 10; i++)
            Assert.Equal(i, sorted[i]);

        // Current item at position 0
        Assert.Equal(3, indices[0]);
    }

    [Fact]
    public void EnableShuffle_ShuffledOrderDiffersFromOriginal()
    {
        // With enough items, extremely unlikely shuffle matches original order
        var items = new ObservableCollection<PlaylistItem>();
        for (var i = 0; i < 20; i++)
            items.Add(new PlaylistItem($@"C:\Videos\v{i}.mp4"));

        var provider = new PlaylistProvider(new Random(42));
        provider.Activate(items, 0);
        provider.EnableShuffle();

        var indices = provider.ShuffledIndices!;
        var isOriginalOrder = true;
        for (var i = 0; i < indices.Count; i++)
        {
            if (indices[i] != i) { isOriginalOrder = false; break; }
        }

        Assert.False(isOriginalOrder);
    }

    // ── Shuffle: Navigation follows shuffled order ──

    [Fact]
    public void Shuffle_GetNextFile_FollowsShuffledOrder()
    {
        var items = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Videos\a.mp4"),
            new(@"C:\Videos\b.mp4"),
            new(@"C:\Videos\c.mp4"),
            new(@"C:\Videos\d.mp4"),
        };

        var provider = new PlaylistProvider(new Random(42));
        provider.Activate(items, 0);
        provider.EnableShuffle();

        // Navigate forward through shuffled order
        var shuffledOrder = provider.ShuffledIndices!.ToList();
        for (var i = 1; i < shuffledOrder.Count; i++)
        {
            var next = provider.GetNextFile();
            Assert.Equal(items[shuffledOrder[i]].FilePath, next);
        }
    }

    [Fact]
    public void Shuffle_GetPreviousFile_FollowsReverseShuffledOrder()
    {
        var items = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Videos\a.mp4"),
            new(@"C:\Videos\b.mp4"),
            new(@"C:\Videos\c.mp4"),
            new(@"C:\Videos\d.mp4"),
        };

        var provider = new PlaylistProvider(new Random(42));
        provider.Activate(items, 0);
        provider.EnableShuffle();

        // Move to end of shuffled order
        for (var i = 1; i < items.Count; i++)
            provider.GetNextFile();

        // Navigate backward
        var shuffledOrder = provider.ShuffledIndices!.ToList();
        for (var i = shuffledOrder.Count - 2; i >= 0; i--)
        {
            var prev = provider.GetPreviousFile();
            Assert.Equal(items[shuffledOrder[i]].FilePath, prev);
        }
    }

    // ── Shuffle: Looping ──

    [Fact]
    public void Shuffle_GetNextFile_LoopsAndReshuffles()
    {
        var items = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Videos\a.mp4"),
            new(@"C:\Videos\b.mp4"),
            new(@"C:\Videos\c.mp4"),
        };

        var provider = new PlaylistProvider(new Random(42));
        provider.Activate(items, 0);
        provider.EnableShuffle();

        var firstShuffleOrder = provider.ShuffledIndices!.ToList();

        // Navigate through all items to complete the loop
        for (var i = 1; i < items.Count; i++)
            provider.GetNextFile();

        // Next call should trigger re-shuffle and return from new order
        var next = provider.GetNextFile();

        Assert.NotNull(next);
        // After re-shuffle, position 0 is the current item, so next should be position 1
        Assert.Equal(1, provider.ShufflePosition);
    }

    [Fact]
    public void Shuffle_GetPreviousFile_WrapsWithoutReshuffle()
    {
        var items = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Videos\a.mp4"),
            new(@"C:\Videos\b.mp4"),
            new(@"C:\Videos\c.mp4"),
        };

        var provider = new PlaylistProvider(new Random(42));
        provider.Activate(items, 0);
        provider.EnableShuffle();

        var shuffledOrder = provider.ShuffledIndices!.ToList();

        // Go backwards from position 0 — should wrap to last position
        var prev = provider.GetPreviousFile();

        Assert.Equal(items[shuffledOrder[^1]].FilePath, prev);
        Assert.Equal(shuffledOrder.Count - 1, provider.ShufflePosition);
    }

    // ── Shuffle: Single item ──

    [Fact]
    public void Shuffle_SingleItem_GetNextFile_ReturnsSame()
    {
        var single = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Videos\only.mp4"),
        };

        var provider = new PlaylistProvider(new Random(42));
        provider.Activate(single, 0);
        provider.EnableShuffle();

        var next = provider.GetNextFile();

        Assert.Equal(@"C:\Videos\only.mp4", next);
    }

    // ── Shuffle: Disable returns to original position ──

    [Fact]
    public void DisableShuffle_RestoresOriginalIndex()
    {
        var items = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Videos\a.mp4"),
            new(@"C:\Videos\b.mp4"),
            new(@"C:\Videos\c.mp4"),
            new(@"C:\Videos\d.mp4"),
        };

        var provider = new PlaylistProvider(new Random(42));
        provider.Activate(items, 0);
        provider.EnableShuffle();

        // Navigate to position 2 in shuffled order
        provider.GetNextFile();
        provider.GetNextFile();

        var shuffledOrder = provider.ShuffledIndices!.ToList();
        var expectedOriginalIndex = shuffledOrder[provider.ShufflePosition];

        provider.DisableShuffle();

        Assert.Equal(expectedOriginalIndex, provider.CurrentIndex);
        Assert.False(provider.IsShuffling);
        Assert.Null(provider.ShuffledIndices);
    }

    [Fact]
    public void DisableShuffle_SequentialNavigationResumesFromCurrentPosition()
    {
        var items = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Videos\a.mp4"),
            new(@"C:\Videos\b.mp4"),
            new(@"C:\Videos\c.mp4"),
            new(@"C:\Videos\d.mp4"),
        };

        var provider = new PlaylistProvider(new Random(42));
        provider.Activate(items, 0);
        provider.EnableShuffle();

        // Navigate to some position
        provider.GetNextFile();

        var currentOriginal = provider.CurrentIndex;

        provider.DisableShuffle();

        // Sequential next should go to currentOriginal + 1
        var next = provider.GetNextFile();
        var expectedIndex = (currentOriginal + 1) % items.Count;
        Assert.Equal(items[expectedIndex].FilePath, next);
    }

    // ── Shuffle: SetCurrentIndex updates shuffle position ──

    [Fact]
    public void SetCurrentIndex_InShuffleMode_UpdatesShufflePosition()
    {
        var items = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Videos\a.mp4"),
            new(@"C:\Videos\b.mp4"),
            new(@"C:\Videos\c.mp4"),
        };

        var provider = new PlaylistProvider(new Random(42));
        provider.Activate(items, 0);
        provider.EnableShuffle();

        var shuffledOrder = provider.ShuffledIndices!.ToList();

        // Set to the original index that's at some position in the shuffle
        var targetIndex = shuffledOrder[2];
        provider.SetCurrentIndex(targetIndex);

        Assert.Equal(2, provider.ShufflePosition);
    }

    // ── Shuffle: RebuildShuffleOrder ──

    [Fact]
    public void RebuildShuffleOrder_PreservesCurrentItem()
    {
        var items = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Videos\a.mp4"),
            new(@"C:\Videos\b.mp4"),
            new(@"C:\Videos\c.mp4"),
        };

        var provider = new PlaylistProvider(new Random(42));
        provider.Activate(items, 1);
        provider.EnableShuffle();

        // Add a new item to the collection
        items.Add(new PlaylistItem(@"C:\Videos\d.mp4"));
        provider.RebuildShuffleOrder();

        // Current item (index 1) should still be at position 0
        Assert.Equal(1, provider.ShuffledIndices![0]);
        Assert.Equal(4, provider.ShuffledIndices.Count);
        Assert.Equal(0, provider.ShufflePosition);
    }

    [Fact]
    public void RebuildShuffleOrder_WhenNotShuffling_DoesNothing()
    {
        _provider.Activate(_items, 0);
        _provider.RebuildShuffleOrder();

        Assert.Null(_provider.ShuffledIndices);
    }

    [Fact]
    public void RebuildShuffleOrder_ClampsIndexWhenItemsRemoved()
    {
        var items = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Videos\a.mp4"),
            new(@"C:\Videos\b.mp4"),
            new(@"C:\Videos\c.mp4"),
        };

        var provider = new PlaylistProvider(new Random(42));
        provider.Activate(items, 2);
        provider.EnableShuffle();

        // Navigate to last item position, then remove items
        items.RemoveAt(2);
        provider.RebuildShuffleOrder();

        // Should have 2 items and valid indices
        Assert.Equal(2, provider.ShuffledIndices!.Count);
        Assert.All(provider.ShuffledIndices, idx => Assert.InRange(idx, 0, 1));
    }

    // ── Shuffle: Persists across deactivation ──

    [Fact]
    public void Shuffle_PersistsAcrossDeactivationReactivation()
    {
        var items = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Videos\a.mp4"),
            new(@"C:\Videos\b.mp4"),
            new(@"C:\Videos\c.mp4"),
        };

        var provider = new PlaylistProvider(new Random(42));
        provider.Activate(items, 0);
        provider.EnableShuffle();
        Assert.True(provider.IsShuffling);

        provider.Deactivate();
        Assert.True(provider.IsShuffling); // Preference persists

        provider.Activate(items, 1);
        Assert.True(provider.IsShuffling);
        Assert.NotNull(provider.ShuffledIndices);
        Assert.Equal(1, provider.ShuffledIndices![0]); // New current item at position 0
    }

    // ── Shuffle: EnableShuffle before activation ──

    [Fact]
    public void EnableShuffle_BeforeActivation_DoesNotBuild()
    {
        _provider.EnableShuffle();

        Assert.True(_provider.IsShuffling);
        Assert.Null(_provider.ShuffledIndices);
    }

    [Fact]
    public void EnableShuffle_BeforeActivation_BuildsOnActivate()
    {
        _provider.EnableShuffle();
        _provider.Activate(_items, 0);

        Assert.NotNull(_provider.ShuffledIndices);
        Assert.Equal(0, _provider.ShuffledIndices![0]);
    }

    // ── Shuffle: Interface dispatch ──

    [Fact]
    public void EnableShuffle_ViaInterfaceReference_DispatchesToConcreteImplementation()
    {
        // This test verifies that calling EnableShuffle() through an IPlaylistProvider
        // reference dispatches to PlaylistProvider's concrete implementation, NOT to
        // the default interface method (no-op). This is the exact call path used by
        // VideoPlayerViewModel when the user toggles shuffle in the control bar.
        IPlaylistProvider provider = new PlaylistProvider();
        var items = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Videos\a.mp4"),
            new(@"C:\Videos\b.mp4"),
            new(@"C:\Videos\c.mp4"),
        };

        // Activate through the concrete type (as the plugin does internally)
        ((PlaylistProvider)provider).Activate(items, 0);

        // Call EnableShuffle through the INTERFACE reference (as the host does)
        provider.EnableShuffle();

        Assert.True(provider.IsShuffling);
    }

    [Fact]
    public void DisableShuffle_ViaInterfaceReference_DispatchesToConcreteImplementation()
    {
        IPlaylistProvider provider = new PlaylistProvider();
        var items = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Videos\a.mp4"),
            new(@"C:\Videos\b.mp4"),
            new(@"C:\Videos\c.mp4"),
        };

        ((PlaylistProvider)provider).Activate(items, 0);
        provider.EnableShuffle();
        Assert.True(provider.IsShuffling);

        provider.DisableShuffle();
        Assert.False(provider.IsShuffling);
    }

    // ── Shuffle: Skips non-video files in shuffled order ──

    [Fact]
    public void Shuffle_SkipsNonVideoFilesInShuffledOrder()
    {
        var mixedItems = new ObservableCollection<PlaylistItem>
        {
            new(@"C:\Videos\a.mp4"),
            new(@"C:\Videos\script.funscript"),
            new(@"C:\Videos\b.mp4"),
            new(@"C:\Videos\c.mp4"),
        };

        var provider = new PlaylistProvider(new Random(42));
        provider.Activate(mixedItems, 0);
        provider.EnableShuffle();

        // Navigate through all — should only get video files
        var results = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var next = provider.GetNextFile();
            if (next is not null) results.Add(next);
        }

        Assert.DoesNotContain(@"C:\Videos\script.funscript", results);
        Assert.All(results, r => Assert.True(PlaylistViewModel.IsVideoFile(r)));
    }
}
