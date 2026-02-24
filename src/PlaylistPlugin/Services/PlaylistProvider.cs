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
/// </summary>
public sealed class PlaylistProvider : IPlaylistProvider
{
    private ObservableCollection<PlaylistItem>? _items;
    private int _currentIndex = -1;
    private bool _isActive;

    /// <inheritdoc />
    public bool IsActive => _isActive;

    /// <summary>
    /// The current playback index within the playlist.
    /// </summary>
    public int CurrentIndex => _currentIndex;

    /// <summary>
    /// Activates the provider and sets the current index.
    /// Called when the user plays an item from the playlist.
    /// </summary>
    public void Activate(ObservableCollection<PlaylistItem> items, int index)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items;
        _currentIndex = index;
        _isActive = true;
    }

    /// <summary>
    /// Deactivates the provider. Vido reverts to built-in navigation.
    /// Called when a non-playlist video is loaded.
    /// </summary>
    public void Deactivate()
    {
        _isActive = false;
        _currentIndex = -1;
    }

    /// <summary>
    /// Updates the current index without changing activation state.
    /// Used when a video load matches a playlist item.
    /// </summary>
    public void SetCurrentIndex(int index)
    {
        _currentIndex = index;
    }

    /// <inheritdoc />
    public string? GetNextFile()
    {
        if (!_isActive || _items is null || _items.Count == 0)
            return null;

        return FindVideoFile(direction: 1);
    }

    /// <inheritdoc />
    public string? GetPreviousFile()
    {
        if (!_isActive || _items is null || _items.Count == 0)
            return null;

        return FindVideoFile(direction: -1);
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
