using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using PlaylistPlugin.Models;
using PlaylistPlugin.Services;
using Vido.Core.Events;
using Vido.Core.Playback;

namespace PlaylistPlugin.ViewModels;

/// <summary>
/// ViewModel for the playlist sidebar panel. Manages the active playlist,
/// UI item collection, playback commands, and drag-and-drop file handling.
/// </summary>
public sealed class PlaylistViewModel : INotifyPropertyChanged
{
    private readonly PlaylistFileService _fileService;
    private readonly IVideoEngine _videoEngine;
    private readonly IEventBus _eventBus;
    private Playlist _currentPlaylist;
    private PlaylistItemViewModel? _currentItem;
    private string _playlistName = string.Empty;
    private IDisposable? _videoLoadedSubscription;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// The underlying playlist model.
    /// </summary>
    public Playlist CurrentPlaylist
    {
        get => _currentPlaylist;
        private set
        {
            if (ReferenceEquals(_currentPlaylist, value)) return;

            // Unhook old playlist events
            if (_currentPlaylist != null)
            {
                _currentPlaylist.Items.CollectionChanged -= OnModelItemsChanged;
            }

            _currentPlaylist = value;

            // Hook new playlist events
            _currentPlaylist.Items.CollectionChanged += OnModelItemsChanged;

            // Rebuild the VM item collection
            RebuildItems();

            PlaylistName = _currentPlaylist.Name;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Observable collection of item ViewModels bound to the list view.
    /// </summary>
    public ObservableCollection<PlaylistItemViewModel> Items { get; } = [];

    /// <summary>
    /// The currently selected / playing item.
    /// </summary>
    public PlaylistItemViewModel? CurrentItem
    {
        get => _currentItem;
        set
        {
            if (ReferenceEquals(_currentItem, value)) return;

            // Clear previous playing indicator
            if (_currentItem != null) _currentItem.IsPlaying = false;

            _currentItem = value;

            // Set new playing indicator
            if (_currentItem != null) _currentItem.IsPlaying = true;

            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Display name for the playlist (editable).
    /// </summary>
    public string PlaylistName
    {
        get => _playlistName;
        set
        {
            if (_playlistName == value) return;
            _playlistName = value;
            _currentPlaylist.Name = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Whether the playlist has any items.
    /// </summary>
    public bool HasItems => Items.Count > 0;

    /// <summary>
    /// Command to create a new empty playlist.
    /// </summary>
    public ICommand NewPlaylistCommand { get; }

    /// <summary>
    /// Command to play a specific item. Parameter: <see cref="PlaylistItemViewModel"/>.
    /// </summary>
    public ICommand PlayItemCommand { get; }

    public PlaylistViewModel(PlaylistFileService fileService, IVideoEngine videoEngine, IEventBus eventBus)
    {
        ArgumentNullException.ThrowIfNull(fileService);
        ArgumentNullException.ThrowIfNull(videoEngine);
        ArgumentNullException.ThrowIfNull(eventBus);

        _fileService = fileService;
        _videoEngine = videoEngine;
        _eventBus = eventBus;

        _currentPlaylist = _fileService.CreateNew();
        _playlistName = _currentPlaylist.Name;
        _currentPlaylist.Items.CollectionChanged += OnModelItemsChanged;

        NewPlaylistCommand = new RelayCommand(ExecuteNewPlaylist);
        PlayItemCommand = new RelayCommand<PlaylistItemViewModel>(ExecutePlayItem);

        // Subscribe to video loaded events to track currently playing item
        _videoLoadedSubscription = _eventBus.Subscribe<VideoLoadedEvent>(OnVideoLoaded);

        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasItems));
    }

    /// <summary>
    /// Adds a single file to the current playlist. Skips duplicates.
    /// </summary>
    public void AddItem(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var item = new PlaylistItem(filePath);

        // Skip duplicates (case-insensitive path comparison via PlaylistItem.Equals)
        if (_currentPlaylist.Items.Contains(item)) return;

        _currentPlaylist.Items.Add(item);
    }

    /// <summary>
    /// Adds multiple files to the current playlist. Skips duplicates.
    /// </summary>
    public void AddItems(IEnumerable<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        foreach (var path in filePaths)
        {
            AddItem(path);
        }
    }

    /// <summary>
    /// Adds a file or folder contents to the playlist from a file explorer context menu action.
    /// For directories, recursively enumerates and adds all files within.
    /// Skips duplicates.
    /// </summary>
    public void AddFromFileNode(string fullPath, bool isDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        if (isDirectory)
        {
            try
            {
                var files = Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories);
                AddItems(files);
            }
            catch (UnauthorizedAccessException)
            {
                // Skip folders we can't access
            }
        }
        else
        {
            AddItem(fullPath);
        }
    }

    /// <summary>
    /// Handles dropped files/folders from Windows Explorer.
    /// For folders, recursively enumerates all files within.
    /// </summary>
    public void HandleFileDrop(string[] droppedPaths)
    {
        ArgumentNullException.ThrowIfNull(droppedPaths);

        var filePaths = new List<string>();

        foreach (var path in droppedPaths)
        {
            if (Directory.Exists(path))
            {
                // Recursively enumerate all files in the dropped folder
                try
                {
                    var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories);
                    filePaths.AddRange(files);
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip folders we can't access
                }
            }
            else if (File.Exists(path))
            {
                filePaths.Add(path);
            }
        }

        AddItems(filePaths);
    }

    /// <summary>
    /// Disposes event subscriptions.
    /// </summary>
    public void Dispose()
    {
        _videoLoadedSubscription?.Dispose();
        _videoLoadedSubscription = null;
    }

    // ── Private Helpers ──

    private void ExecuteNewPlaylist()
    {
        CurrentPlaylist = _fileService.CreateNew();
        CurrentItem = null;
    }

    private void ExecutePlayItem(PlaylistItemViewModel? item)
    {
        if (item == null || !item.FileExists) return;

        CurrentItem = item;
        _eventBus.Publish(new PlayFileRequestedEvent { FilePath = item.FilePath });
    }

    private void OnVideoLoaded(VideoLoadedEvent e)
    {
        // Find the item matching the loaded file and mark it as playing
        var match = Items.FirstOrDefault(i =>
            string.Equals(i.FilePath, e.FilePath, StringComparison.OrdinalIgnoreCase));

        CurrentItem = match;
    }

    private void OnModelItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems != null)
                {
                    var index = e.NewStartingIndex;
                    foreach (PlaylistItem item in e.NewItems)
                    {
                        Items.Insert(index++, new PlaylistItemViewModel(item));
                    }
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null)
                {
                    foreach (PlaylistItem item in e.OldItems)
                    {
                        var vm = Items.FirstOrDefault(i =>
                            string.Equals(i.FilePath, item.FilePath, StringComparison.OrdinalIgnoreCase));
                        if (vm != null) Items.Remove(vm);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                Items.Clear();
                break;

            default:
                RebuildItems();
                break;
        }
    }

    private void RebuildItems()
    {
        Items.Clear();
        foreach (var item in _currentPlaylist.Items)
        {
            Items.Add(new PlaylistItemViewModel(item));
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // ── Nested Command Implementations ──

    private sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }

    private sealed class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        public RelayCommand(Action<T?> execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute(parameter is T t ? t : default);
    }
}
