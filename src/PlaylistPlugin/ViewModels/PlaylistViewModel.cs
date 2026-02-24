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
using Vido.Core.Plugin;

namespace PlaylistPlugin.ViewModels;

/// <summary>
/// ViewModel for the playlist sidebar panel. Manages the active playlist,
/// UI item collection, playback commands, drag-and-drop file handling,
/// save/load/recent playlists, and status bar text.
/// </summary>
public sealed class PlaylistViewModel : INotifyPropertyChanged
{
    private readonly PlaylistFileService _fileService;
    private readonly IVideoEngine _videoEngine;
    private readonly IEventBus _eventBus;
    private readonly IDialogService _dialogService;
    private readonly IPluginSettingsStore? _settings;
    private readonly Action<string>? _updateStatusBar;
    private readonly ToastService? _toastService;

    private Playlist _currentPlaylist;
    private PlaylistItemViewModel? _currentItem;
    private string _playlistName = string.Empty;
    private string _statusText = string.Empty;
    private IDisposable? _videoLoadedSubscription;

    private const string PlaylistFilter = "Vido Playlist (*.vidpl)|*.vidpl";
    private const string RecentPlaylistsKey = "recentPlaylists";
    private const string LastPlaylistPathKey = "lastPlaylistPath";
    private const int MaxRecentPlaylists = 10;

    /// <summary>
    /// Supported video file extensions (matches Vido's FileNode.VideoExtensions).
    /// </summary>
    internal static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm"
    };

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
    /// Status bar text showing playlist info.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value) return;
            _statusText = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Recent playlist file paths for the dropdown.
    /// </summary>
    public ObservableCollection<string> RecentPlaylists { get; } = [];

    /// <summary>
    /// Command to create a new empty playlist.
    /// </summary>
    public ICommand NewPlaylistCommand { get; }

    /// <summary>
    /// Command to play a specific item. Parameter: <see cref="PlaylistItemViewModel"/>.
    /// </summary>
    public ICommand PlayItemCommand { get; }

    /// <summary>
    /// Command to open a playlist file via browse dialog.
    /// </summary>
    public ICommand OpenPlaylistCommand { get; }

    /// <summary>
    /// Command to save the current playlist. Prompts browse if never saved.
    /// </summary>
    public ICommand SavePlaylistCommand { get; }

    /// <summary>
    /// Command to save the current playlist with a new name (always prompts browse).
    /// </summary>
    public ICommand SavePlaylistAsCommand { get; }

    /// <summary>
    /// Command to open a recent playlist. Parameter: file path string.
    /// </summary>
    public ICommand OpenRecentPlaylistCommand { get; }

    public PlaylistViewModel(
        PlaylistFileService fileService,
        IVideoEngine videoEngine,
        IEventBus eventBus,
        IDialogService dialogService,
        IPluginSettingsStore? settings = null,
        Action<string>? updateStatusBar = null,
        ToastService? toastService = null)
    {
        ArgumentNullException.ThrowIfNull(fileService);
        ArgumentNullException.ThrowIfNull(videoEngine);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(dialogService);

        _fileService = fileService;
        _videoEngine = videoEngine;
        _eventBus = eventBus;
        _dialogService = dialogService;
        _settings = settings;
        _updateStatusBar = updateStatusBar;
        _toastService = toastService;

        _currentPlaylist = _fileService.CreateNew();
        _playlistName = _currentPlaylist.Name;
        _currentPlaylist.Items.CollectionChanged += OnModelItemsChanged;

        NewPlaylistCommand = new RelayCommand(ExecuteNewPlaylist);
        PlayItemCommand = new RelayCommand<PlaylistItemViewModel>(ExecutePlayItem);
        OpenPlaylistCommand = new RelayCommand(ExecuteOpenPlaylist);
        SavePlaylistCommand = new RelayCommand(ExecuteSavePlaylist);
        SavePlaylistAsCommand = new RelayCommand(ExecuteSavePlaylistAs);
        OpenRecentPlaylistCommand = new RelayCommand<string>(ExecuteOpenRecentPlaylist);

        // Subscribe to video loaded events to track currently playing item
        _videoLoadedSubscription = _eventBus.Subscribe<VideoLoadedEvent>(OnVideoLoaded);

        Items.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasItems));
            UpdateStatusText();
        };

        LoadRecentPlaylists();
        UpdateStatusText();
        RestoreLastPlaylist();
    }

    /// <summary>
    /// Adds a single file to the current playlist. Skips duplicates.
    /// </summary>
    public void AddItem(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // Only allow video files
        if (!IsVideoFile(filePath)) return;

        var item = new PlaylistItem(filePath);

        // Skip duplicates (case-insensitive path comparison via PlaylistItem.Equals)
        if (_currentPlaylist.Items.Contains(item)) return;

        _currentPlaylist.Items.Add(item);
    }

    /// <summary>
    /// Returns true if the file has a supported video extension.
    /// </summary>
    public static bool IsVideoFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return !string.IsNullOrEmpty(ext) && VideoExtensions.Contains(ext);
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

        ShowToast("Added to ", _playlistName);
    }

    /// <summary>
    /// Shows a brief toast message on the Vido main window.
    /// </summary>
    public void ShowToast(string message, string? boldSuffix = null)
    {
        _toastService?.Show(message, boldSuffix);
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
        if (!PromptSaveDirtyPlaylist()) return;
        CurrentPlaylist = _fileService.CreateNew();
        CurrentItem = null;
        UpdateStatusText();
    }

    private void ExecutePlayItem(PlaylistItemViewModel? item)
    {
        if (item == null || !item.FileExists) return;

        CurrentItem = item;
        _eventBus.Publish(new PlayFileRequestedEvent { FilePath = item.FilePath });
        UpdateStatusText();
    }

    private async void ExecuteOpenPlaylist()
    {
        if (!PromptSaveDirtyPlaylist()) return;

        var path = _dialogService.ShowOpenFileDialog(PlaylistFilter);
        if (path is null) return;

        await LoadPlaylistFromPathAsync(path);
    }

    private async void ExecuteSavePlaylist()
    {
        await SaveCurrentPlaylistAsync(saveAs: false);
    }

    private async void ExecuteSavePlaylistAs()
    {
        await SaveCurrentPlaylistAsync(saveAs: true);
    }

    private async void ExecuteOpenRecentPlaylist(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        if (!File.Exists(path))
        {
            // Remove stale entry
            RemoveRecentPlaylist(path);
            return;
        }

        if (!PromptSaveDirtyPlaylist()) return;

        await LoadPlaylistFromPathAsync(path);
    }

    /// <summary>
    /// Loads a playlist from disk and sets it as the current playlist.
    /// </summary>
    internal async Task LoadPlaylistFromPathAsync(string path)
    {
        try
        {
            var playlist = await _fileService.LoadAsync(path);
            CurrentPlaylist = playlist;
            CurrentItem = null;

            // Derive display name from file name
            PlaylistName = Path.GetFileNameWithoutExtension(path);
            _currentPlaylist.Name = PlaylistName;

            AddRecentPlaylist(path);
            PersistLastPlaylistPath(path);
            UpdateStatusText();
        }
        catch
        {
            // Load failures are handled gracefully — playlist stays unchanged
        }
    }

    /// <summary>
    /// Saves the current playlist to disk. If saveAs is true or FilePath is null, prompts browse dialog.
    /// Returns true if saved successfully, false if cancelled or failed.
    /// </summary>
    internal async Task<bool> SaveCurrentPlaylistAsync(bool saveAs)
    {
        var path = _currentPlaylist.FilePath;

        if (saveAs || string.IsNullOrEmpty(path))
        {
            path = _dialogService.ShowSaveFileDialog(_currentPlaylist.Name, PlaylistFilter);
            if (path is null) return false;
        }

        try
        {
            // Update name from file name
            var nameFromFile = Path.GetFileNameWithoutExtension(path);
            _currentPlaylist.Name = nameFromFile;
            PlaylistName = nameFromFile;

            await _fileService.SaveAsync(_currentPlaylist, path);
            AddRecentPlaylist(path);
            PersistLastPlaylistPath(path);
            UpdateStatusText();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// If the current playlist is dirty, prompts the user to save.
    /// Returns true to proceed, false to cancel.
    /// </summary>
    internal bool PromptSaveDirtyPlaylist()
    {
        if (!_currentPlaylist.IsDirty) return true;

        var result = _dialogService.ShowConfirmationDialog(
            $"Save changes to \"{_currentPlaylist.Name}\"?",
            "Unsaved Changes");

        switch (result)
        {
            case true:
                // Save — block on save
                var saved = SaveCurrentPlaylistAsync(saveAs: false).GetAwaiter().GetResult();
                return saved;
            case false:
                // Don't save — discard and proceed
                return true;
            default:
                // Cancel
                return false;
        }
    }

    // ── Recent Playlists ──

    private void LoadRecentPlaylists()
    {
        RecentPlaylists.Clear();
        var stored = _settings?.Get(RecentPlaylistsKey, string.Empty) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(stored)) return;

        var paths = stored.Split('|', StringSplitOptions.RemoveEmptyEntries);
        foreach (var path in paths)
        {
            if (File.Exists(path))
                RecentPlaylists.Add(path);
        }
    }

    internal void AddRecentPlaylist(string path)
    {
        // Remove if already exists (to move to top)
        for (var i = RecentPlaylists.Count - 1; i >= 0; i--)
        {
            if (string.Equals(RecentPlaylists[i], path, StringComparison.OrdinalIgnoreCase))
                RecentPlaylists.RemoveAt(i);
        }

        RecentPlaylists.Insert(0, path);

        // Trim to max
        while (RecentPlaylists.Count > MaxRecentPlaylists)
            RecentPlaylists.RemoveAt(RecentPlaylists.Count - 1);

        SaveRecentPlaylists();
    }

    private void RemoveRecentPlaylist(string path)
    {
        for (var i = RecentPlaylists.Count - 1; i >= 0; i--)
        {
            if (string.Equals(RecentPlaylists[i], path, StringComparison.OrdinalIgnoreCase))
                RecentPlaylists.RemoveAt(i);
        }
        SaveRecentPlaylists();
    }

    private void SaveRecentPlaylists()
    {
        var joined = string.Join("|", RecentPlaylists);
        _settings?.Set(RecentPlaylistsKey, joined);
    }

    private void PersistLastPlaylistPath(string path)
    {
        _settings?.Set(LastPlaylistPathKey, path);
    }

    private async void RestoreLastPlaylist()
    {
        var lastPath = _settings?.Get(LastPlaylistPathKey, string.Empty) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(lastPath) && File.Exists(lastPath))
        {
            await LoadPlaylistFromPathAsync(lastPath);
        }
    }

    // ── Status Bar ──

    internal void UpdateStatusText()
    {
        string text;
        if (_currentItem != null)
        {
            var index = Items.IndexOf(_currentItem);
            text = $"Playing {index + 1} of {Items.Count} — {_playlistName}";
        }
        else
        {
            text = $"{_playlistName} — {Items.Count} items";
        }

        StatusText = text;
        _updateStatusBar?.Invoke(text);
    }

    private void OnVideoLoaded(VideoLoadedEvent e)
    {
        // Find the item matching the loaded file and mark it as playing
        var match = Items.FirstOrDefault(i =>
            string.Equals(i.FilePath, e.FilePath, StringComparison.OrdinalIgnoreCase));

        CurrentItem = match;
        UpdateStatusText();
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
