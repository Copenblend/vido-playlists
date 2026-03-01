using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PlaylistPlugin.Models;

/// <summary>
/// Represents a playlist containing an ordered collection of <see cref="PlaylistItem"/> entries.
/// Tracks unsaved changes via <see cref="IsDirty"/> and notifies via <see cref="INotifyPropertyChanged"/>.
/// </summary>
public sealed class Playlist : INotifyPropertyChanged
{
    private string _name;
    private string? _filePath;
    private bool _isDirty;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// User-defined playlist name.
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            IsDirty = true;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Ordered list of items in the playlist.
    /// </summary>
    public RangeObservableCollection<PlaylistItem> Items { get; }

    /// <summary>
    /// Path where the playlist file is saved on disk.
    /// <c>null</c> if the playlist has never been saved.
    /// </summary>
    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (_filePath == value) return;
            _filePath = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Whether the playlist has unsaved changes.
    /// </summary>
    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty == value) return;
            _isDirty = value;
            OnPropertyChanged();
        }
    }

    public Playlist(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
        Items = [];
        Items.CollectionChanged += OnItemsCollectionChanged;
    }

    public Playlist(string name, IEnumerable<PlaylistItem> items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
        Items = new RangeObservableCollection<PlaylistItem>(items);
        Items.CollectionChanged += OnItemsCollectionChanged;
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        IsDirty = true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
