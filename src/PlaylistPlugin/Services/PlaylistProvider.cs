using System.Collections.ObjectModel;
using PlaylistPlugin.Models;
using PlaylistPlugin.ViewModels;
using Vido.Core.Plugin;

namespace PlaylistPlugin.Services;

/// <summary>
/// Implements <see cref="IPlaylistProvider"/> to integrate playlist next/previous
/// navigation with Vido's built-in transport controls. When active, Vido delegates
/// SkipNext, SkipPrevious, and auto-advance-on-media-ended to this provider.
/// Navigation wraps (loops) from last→first and first→last.
/// Only video files are navigated; non-video items are skipped.
/// Supports shuffle mode with Fisher-Yates shuffled playback order.
/// </summary>
public sealed class PlaylistProvider : IPlaylistProvider
{
    private ObservableCollection<PlaylistItem>? _items;
    private int _currentIndex = -1;
    private bool _isActive;
    private bool _isShuffling;
    private List<int>? _shuffledIndices;
    private int _shufflePosition;
    private readonly Random _random;

    public PlaylistProvider() : this(new Random()) { }

    /// <summary>
    /// Constructor accepting a <see cref="Random"/> instance for deterministic testing.
    /// </summary>
    internal PlaylistProvider(Random random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <inheritdoc />
    public bool IsActive => _isActive;

    /// <summary>
    /// The current playback index within the playlist (original order).
    /// </summary>
    public int CurrentIndex => _currentIndex;

    /// <summary>
    /// Whether shuffle mode is enabled.
    /// </summary>
    public bool IsShuffling => _isShuffling;

    /// <summary>
    /// Read-only access to the current shuffled index order (for testing).
    /// </summary>
    internal IReadOnlyList<int>? ShuffledIndices => _shuffledIndices;

    /// <summary>
    /// Current position within the shuffled order (for testing).
    /// </summary>
    internal int ShufflePosition => _shufflePosition;

    /// <summary>
    /// Activates the provider and sets the current index.
    /// Called when the user plays an item from the playlist.
    /// If shuffle is enabled, rebuilds the shuffled order.
    /// </summary>
    public void Activate(ObservableCollection<PlaylistItem> items, int index)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items;
        _currentIndex = index;
        _isActive = true;

        if (_isShuffling)
            BuildShuffleOrder();
    }

    /// <summary>
    /// Deactivates the provider. Vido reverts to built-in navigation.
    /// Shuffle preference is preserved across deactivation.
    /// </summary>
    public void Deactivate()
    {
        _isActive = false;
        _currentIndex = -1;
    }

    /// <summary>
    /// Updates the current index without changing activation state.
    /// In shuffle mode, also updates the shuffle position.
    /// </summary>
    public void SetCurrentIndex(int index)
    {
        _currentIndex = index;
        if (_isShuffling && _shuffledIndices is not null)
        {
            var pos = _shuffledIndices.IndexOf(index);
            if (pos >= 0)
                _shufflePosition = pos;
        }
    }

    /// <summary>
    /// Enables shuffle mode. Builds a Fisher-Yates shuffled order
    /// with the currently playing item placed first.
    /// </summary>
    public void EnableShuffle()
    {
        _isShuffling = true;
        if (_isActive && _items is not null && _items.Count > 0 && _currentIndex >= 0)
            BuildShuffleOrder();
    }

    /// <summary>
    /// Disables shuffle mode and returns to sequential playback
    /// at the current item's original position.
    /// </summary>
    public void DisableShuffle()
    {
        if (_isShuffling && _shuffledIndices is not null
            && _shufflePosition >= 0 && _shufflePosition < _shuffledIndices.Count)
        {
            _currentIndex = _shuffledIndices[_shufflePosition];
        }
        _isShuffling = false;
        _shuffledIndices = null;
        _shufflePosition = 0;
    }

    /// <summary>
    /// Rebuilds the shuffle order when playlist items change.
    /// Preserves the currently playing item at position 0.
    /// </summary>
    public void RebuildShuffleOrder()
    {
        if (!_isShuffling || !_isActive || _items is null || _items.Count == 0)
            return;

        // Preserve current playing item's original index
        if (_shuffledIndices is not null && _shufflePosition >= 0 && _shufflePosition < _shuffledIndices.Count)
        {
            _currentIndex = _shuffledIndices[_shufflePosition];
        }

        // Clamp currentIndex if items were removed
        if (_currentIndex >= _items.Count)
            _currentIndex = _items.Count > 0 ? 0 : -1;

        if (_currentIndex >= 0)
            BuildShuffleOrder();
    }

    /// <inheritdoc />
    public string? GetNextFile()
    {
        if (!_isActive || _items is null || _items.Count == 0)
            return null;

        if (_isShuffling && _shuffledIndices is not null)
            return FindVideoFileShuffle(direction: 1);

        return FindVideoFile(direction: 1);
    }

    /// <inheritdoc />
    public string? GetPreviousFile()
    {
        if (!_isActive || _items is null || _items.Count == 0)
            return null;

        if (_isShuffling && _shuffledIndices is not null)
            return FindVideoFileShuffle(direction: -1);

        return FindVideoFile(direction: -1);
    }

    /// <summary>
    /// Builds a Fisher-Yates shuffled order of all item indices,
    /// with the current item placed at position 0.
    /// </summary>
    private void BuildShuffleOrder()
    {
        var count = _items!.Count;
        _shuffledIndices = new List<int>(count);

        // Collect all indices except the current item
        for (var i = 0; i < count; i++)
        {
            if (i != _currentIndex)
                _shuffledIndices.Add(i);
        }

        // Fisher-Yates shuffle
        for (var i = _shuffledIndices.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (_shuffledIndices[i], _shuffledIndices[j]) = (_shuffledIndices[j], _shuffledIndices[i]);
        }

        // Place current item at position 0
        _shuffledIndices.Insert(0, _currentIndex);
        _shufflePosition = 0;
    }

    /// <summary>
    /// Navigates through the shuffled order, skipping non-video files.
    /// On forward loop completion, re-shuffles for the next cycle.
    /// </summary>
    private string? FindVideoFileShuffle(int direction)
    {
        var count = _shuffledIndices!.Count;
        var pos = _shufflePosition;
        var reshuffled = false;

        for (var i = 0; i < count; i++)
        {
            pos += direction;

            if (pos >= count)
            {
                if (!reshuffled && direction > 0)
                {
                    // Completed full forward loop — re-shuffle for next cycle
                    _currentIndex = _shuffledIndices[_shufflePosition];
                    BuildShuffleOrder();
                    reshuffled = true;
                    pos = count > 1 ? 1 : 0;
                }
                else
                {
                    pos = 0;
                }
            }
            else if (pos < 0)
            {
                pos = count - 1;
            }

            var originalIndex = _shuffledIndices[pos];
            var item = _items![originalIndex];

            if (PlaylistViewModel.IsVideoFile(item.FilePath))
            {
                _shufflePosition = pos;
                _currentIndex = originalIndex;
                return item.FilePath;
            }
        }

        return null;
    }

    /// <summary>
    /// Searches for the next video file in the given direction, wrapping around.
    /// Returns null if no video file is found after checking all items.
    /// </summary>
    private string? FindVideoFile(int direction)
    {
        var count = _items!.Count;

        for (var i = 1; i <= count; i++)
        {
            var candidateIndex = ((_currentIndex + direction * i) % count + count) % count;
            var item = _items[candidateIndex];

            if (PlaylistViewModel.IsVideoFile(item.FilePath))
            {
                _currentIndex = candidateIndex;
                return item.FilePath;
            }
        }

        return null;
    }
}
