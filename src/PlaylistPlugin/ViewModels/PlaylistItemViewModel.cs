using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using PlaylistPlugin.Models;

namespace PlaylistPlugin.ViewModels;

/// <summary>
/// ViewModel wrapping a <see cref="PlaylistItem"/> for display in the playlist sidebar.
/// </summary>
public sealed class PlaylistItemViewModel : INotifyPropertyChanged
{
    private bool _isPlaying;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Display name derived from the file path.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Absolute path to the file on disk.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Whether this item is currently being played.
    /// </summary>
    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (_isPlaying == value) return;
            _isPlaying = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Whether the referenced file exists on disk.
    /// Used to gray-out missing files in the UI.
    /// </summary>
    public bool FileExists => File.Exists(FilePath);

    /// <summary>
    /// Tooltip text showing the full path and existence status.
    /// </summary>
    public string ToolTipText => FileExists
        ? FilePath
        : $"{FilePath} (file not found)";

    /// <summary>
    /// The underlying model item.
    /// </summary>
    internal PlaylistItem Model { get; }

    public PlaylistItemViewModel(PlaylistItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Model = item;
        FileName = item.FileName;
        FilePath = item.FilePath;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
