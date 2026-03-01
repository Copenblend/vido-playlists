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
    private readonly PlaylistProvider? _playlistProvider;

    private Playlist _currentPlaylist;
    private PlaylistItemViewModel? _currentItem;
    private readonly HashSet<string> _pathIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PlaylistItemViewModel> _vmIndex = new(StringComparer.OrdinalIgnoreCase);
    private int _currentItemIndex = -1;
    private CancellationTokenSource? _autoSaveCts;
    private bool _recentPlaylistsLoaded;
    private string _playlistName = string.Empty;
    private string _statusText = string.Empty;
    private IDisposable? _videoLoadedSubscription;

    private const string PlaylistFilter = "Vido Playlist (*.vidpl)|*.vidpl";
    private const string RecentPlaylistsKey = "recentPlaylists";
    private const string LastPlaylistPathKey = "lastPlaylistPath";
    private const string AutoSaveKey = "autoSave";
    private const int MaxRecentPlaylists = 10;
    private const int AutoSaveDebounceMs = 500;

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
    public RangeObservableCollection<PlaylistItemViewModel> Items { get; } = [];

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

            _currentItemIndex = _currentItem is not null ? Items.IndexOf(_currentItem) : -1;

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

    /// <summary>
    /// Command to remove an item from the playlist. Parameter: <see cref="PlaylistItemViewModel"/>.
    /// </summary>
    public ICommand RemoveItemCommand { get; }

    /// <summary>
    /// Command to move an item up one position. Parameter: <see cref="PlaylistItemViewModel"/>.
    /// </summary>
    public ICommand MoveUpCommand { get; }

    /// <summary>
    /// Command to move an item down one position. Parameter: <see cref="PlaylistItemViewModel"/>.
    /// </summary>
    public ICommand MoveDownCommand { get; }

    /// <summary>
    /// Command to move an item to the top of the list. Parameter: <see cref="PlaylistItemViewModel"/>.
    /// </summary>
    public ICommand MoveToTopCommand { get; }

    /// <summary>
    /// Command to move an item to the bottom of the list. Parameter: <see cref="PlaylistItemViewModel"/>.
    /// </summary>
    public ICommand MoveToBottomCommand { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistViewModel"/> class.
    /// </summary>
    /// <param name="fileService">Playlist file persistence service.</param>
    /// <param name="videoEngine">Video engine used by the host application.</param>
    /// <param name="eventBus">Event bus used for playback events.</param>
    /// <param name="dialogService">Dialog service for file and confirmation prompts.</param>
    /// <param name="settings">Optional plugin settings store.</param>
    /// <param name="updateStatusBar">Optional callback that updates the host status bar item.</param>
    /// <param name="toastService">Optional toast notification service.</param>
    /// <param name="playlistProvider">Optional playlist provider for next/previous navigation.</param>
    public PlaylistViewModel(
        PlaylistFileService fileService,
        IVideoEngine videoEngine,
        IEventBus eventBus,
        IDialogService dialogService,
        IPluginSettingsStore? settings = null,
        Action<string>? updateStatusBar = null,
        ToastService? toastService = null,
        PlaylistProvider? playlistProvider = null)
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
        _playlistProvider = playlistProvider;

        _currentPlaylist = _fileService.CreateNew();
        _playlistName = _currentPlaylist.Name;
        _currentPlaylist.Items.CollectionChanged += OnModelItemsChanged;

        NewPlaylistCommand = new RelayCommand(ExecuteNewPlaylist);
        PlayItemCommand = new RelayCommand<PlaylistItemViewModel>(ExecutePlayItem);
        OpenPlaylistCommand = new RelayCommand(ExecuteOpenPlaylist);
        SavePlaylistCommand = new RelayCommand(ExecuteSavePlaylist);
        SavePlaylistAsCommand = new RelayCommand(ExecuteSavePlaylistAs);
        OpenRecentPlaylistCommand = new RelayCommand<string>(ExecuteOpenRecentPlaylist);
        RemoveItemCommand = new RelayCommand<PlaylistItemViewModel>(ExecuteRemoveItem);
        MoveUpCommand = new RelayCommand<PlaylistItemViewModel>(ExecuteMoveUp);
        MoveDownCommand = new RelayCommand<PlaylistItemViewModel>(ExecuteMoveDown);
        MoveToTopCommand = new RelayCommand<PlaylistItemViewModel>(ExecuteMoveToTop);
        MoveToBottomCommand = new RelayCommand<PlaylistItemViewModel>(ExecuteMoveToBottom);

        // Subscribe to video loaded events to track currently playing item
        _videoLoadedSubscription = _eventBus.Subscribe<VideoLoadedEvent>(OnVideoLoaded);

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

        if (!_pathIndex.Add(filePath)) return;

        _currentPlaylist.Items.Add(new PlaylistItem(filePath));
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

        var newItems = filePaths
            .Where(path => IsVideoFile(path) && _pathIndex.Add(path))
            .Select(path => new PlaylistItem(path))
            .ToList();

        if (newItems.Count == 0) return;

        _currentPlaylist.Items.AddRange(newItems);
    }

    /// <summary>
    /// Adds a file or folder contents to the playlist from a file explorer context menu action.
    /// For directories, recursively enumerates and adds all files within.
    /// Skips duplicates.
    /// </summary>
    public async void AddFromFileNode(string fullPath, bool isDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        if (isDirectory)
        {
            var files = await Task.Run(() => EnumerateVideoFilesFromDirectory(fullPath));
            if (files.Count > 0)
                AddItems(files);
        }
        else
        {
            AddItem(fullPath);
        }

        ShowToast("Added to ", _playlistName);
        AutoSaveIfEnabled();
    }

    /// <summary>
    /// Removes an item from the playlist.
    /// </summary>
    public void RemoveItem(PlaylistItemViewModel? item)
    {
        if (item is null) return;

        if (ReferenceEquals(item, _currentItem))
            CurrentItem = null;

        _pathIndex.Remove(item.FilePath);
        _currentPlaylist.Items.Remove(item.Model);
        AutoSaveIfEnabled();
    }

    /// <summary>
    /// Moves a playlist item from one index to another (for drag-and-drop reordering).
    /// </summary>
    public void MoveItem(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Items.Count) return;
        if (toIndex < 0 || toIndex >= Items.Count) return;
        if (fromIndex == toIndex) return;

        _currentPlaylist.Items.Move(fromIndex, toIndex);
        AutoSaveIfEnabled();
    }

    /// <summary>
    /// Shows a toast on the Vido main window.
    /// </summary>
    /// <param name="message">Primary toast message.</param>
    /// <param name="boldSuffix">Optional highlighted suffix text.</param>
    public void ShowToast(string message, string? boldSuffix = null)
    {
        _toastService?.Show(message, boldSuffix);
    }

    /// <summary>
    /// Shows an error toast (red) on the Vido main window.
    /// </summary>
    /// <param name="message">Primary toast message.</param>
    /// <param name="boldSuffix">Optional highlighted suffix text.</param>
    public void ShowErrorToast(string message, string? boldSuffix = null)
    {
        _toastService?.ShowError(message, boldSuffix);
    }

    /// <summary>
    /// Handles dropped files/folders from Windows Explorer.
    /// For folders, recursively enumerates all files within.
    /// Supports .vidpl playlist files (opens them) and video files (adds to playlist).
    /// Shows error toast for unsupported file types.
    /// </summary>
    public async void HandleFileDrop(string[] droppedPaths)
    {
        ArgumentNullException.ThrowIfNull(droppedPaths);

        // Check for .vidpl files first — open the first one found
        var vidplFile = droppedPaths.FirstOrDefault(p =>
            string.Equals(Path.GetExtension(p), ".vidpl", StringComparison.OrdinalIgnoreCase)
            && File.Exists(p));

        if (vidplFile is not null)
        {
            if (!PromptSaveDirtyPlaylist()) return;
            _ = LoadPlaylistFromPathAsync(vidplFile);
            return;
        }

        var (filePaths, hasUnsupported) = await Task.Run(() => CollectDroppedVideoFiles(droppedPaths));

        if (filePaths.Count > 0)
        {
            AddItems(filePaths);
            AutoSaveIfEnabled();
        }

        if (hasUnsupported && filePaths.Count == 0)
        {
            ShowErrorToast("Unsupported file type");
        }
        else if (hasUnsupported)
        {
            ShowErrorToast("Some files were skipped (unsupported)");
        }
    }

    /// <summary>
    /// Disposes event subscriptions.
    /// </summary>
    public void Dispose()
    {
        _autoSaveCts?.Cancel();
        _autoSaveCts?.Dispose();
        _autoSaveCts = null;

        _videoLoadedSubscription?.Dispose();
        _videoLoadedSubscription = null;
    }

    /// <summary>
    /// If auto-save is enabled, saves the playlist automatically.
    /// Prompts for a save location if the playlist has never been saved.
    /// </summary>
    internal void AutoSaveIfEnabled()
    {
        var autoSave = _settings?.Get(AutoSaveKey, false) ?? false;
        if (!autoSave) return;

        _autoSaveCts?.Cancel();
        _autoSaveCts?.Dispose();
        _autoSaveCts = new CancellationTokenSource();

        _ = DebounceAutoSaveAsync(_autoSaveCts.Token);
    }

    private async Task DebounceAutoSaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(AutoSaveDebounceMs, cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
                await SaveCurrentPlaylistAsync(saveAs: false);
        }
        catch (OperationCanceledException)
        {
            // Expected when debounce is reset or ViewModel is disposed.
        }
    }

    // ── Private Helpers ──

    private void ExecuteRemoveItem(PlaylistItemViewModel? item) => RemoveItem(item);

    private void ExecuteMoveUp(PlaylistItemViewModel? item)
    {
        if (item is null) return;
        var index = Items.IndexOf(item);
        if (index > 0) MoveItem(index, index - 1);
    }

    private void ExecuteMoveDown(PlaylistItemViewModel? item)
    {
        if (item is null) return;
        var index = Items.IndexOf(item);
        if (index >= 0 && index < Items.Count - 1) MoveItem(index, index + 1);
    }

    private void ExecuteMoveToTop(PlaylistItemViewModel? item)
    {
        if (item is null) return;
        var index = Items.IndexOf(item);
        if (index > 0) MoveItem(index, 0);
    }

    private void ExecuteMoveToBottom(PlaylistItemViewModel? item)
    {
        if (item is null) return;
        var index = Items.IndexOf(item);
        if (index >= 0 && index < Items.Count - 1) MoveItem(index, Items.Count - 1);
    }

    private void ExecuteNewPlaylist()
    {
        if (!PromptSaveDirtyPlaylist()) return;
        CurrentPlaylist = _fileService.CreateNew();
        CurrentItem = null;
        _playlistProvider?.Deactivate();
        UpdateStatusText();
    }

    private void ExecutePlayItem(PlaylistItemViewModel? item)
    {
        if (item == null || !item.FileExists) return;

        CurrentItem = item;

        // Activate playlist provider so Vido delegates next/previous to us
        var index = Items.IndexOf(item);
        if (index >= 0)
            _playlistProvider?.Activate(_currentPlaylist.Items, index);

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
        EnsureRecentPlaylistsLoaded();

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
            _playlistProvider?.Deactivate();

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

    /// <summary>
    /// Loads recent playlists the first time they are needed.
    /// </summary>
    internal void EnsureRecentPlaylistsLoaded()
    {
        if (_recentPlaylistsLoaded) return;

        _recentPlaylistsLoaded = true;
        LoadRecentPlaylists();
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
        try
        {
            var lastPath = _settings?.Get(LastPlaylistPathKey, string.Empty) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(lastPath) && File.Exists(lastPath))
            {
                await LoadPlaylistFromPathAsync(lastPath);
            }
        }
        catch
        {
            // Best-effort restore only. Ignore startup restore failures.
        }
    }

    // ── Status Bar ──

    internal void UpdateStatusText()
    {
        string text;
        if (_currentItem != null)
        {
            text = $"Playing {_currentItemIndex + 1} of {Items.Count} — {_playlistName}";
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
        _vmIndex.TryGetValue(e.FilePath, out var match);

        CurrentItem = match;

        if (match is not null && _playlistProvider is not null)
        {
            // Video is in the playlist — update the provider's index
            if (_playlistProvider.IsActive)
                _playlistProvider.SetCurrentIndex(_currentItemIndex);
        }
        else if (match is null && _playlistProvider is not null)
        {
            // Video is NOT in the playlist — deactivate playlist mode
            _playlistProvider.Deactivate();
        }

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
                        var vm = new PlaylistItemViewModel(item);
                        Items.Insert(index++, vm);
                        _vmIndex[vm.FilePath] = vm;
                        _pathIndex.Add(vm.FilePath);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null)
                {
                    foreach (PlaylistItem item in e.OldItems)
                    {
                        _pathIndex.Remove(item.FilePath);

                        if (_vmIndex.TryGetValue(item.FilePath, out var vm))
                        {
                            _vmIndex.Remove(item.FilePath);
                            if (ReferenceEquals(vm, _currentItem))
                                CurrentItem = null;

                            Items.Remove(vm);
                        }
                    }
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                RebuildItems();
                break;

            case NotifyCollectionChangedAction.Move:
                if (e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0)
                {
                    Items.Move(e.OldStartingIndex, e.NewStartingIndex);
                    if (_currentItem is not null)
                        _currentItemIndex = Items.IndexOf(_currentItem);
                }
                break;

            default:
                RebuildItems();
                break;
        }

        // Rebuild shuffle order when playlist items change
        _playlistProvider?.RebuildShuffleOrder();
        OnPropertyChanged(nameof(HasItems));
        UpdateStatusText();
    }

    private void RebuildItems()
    {
        var currentFilePath = _currentItem?.FilePath;

        var viewModels = _currentPlaylist.Items
            .Select(item => new PlaylistItemViewModel(item));

        Items.ReplaceAll(viewModels);

        _vmIndex.Clear();
        _pathIndex.Clear();

        foreach (var vm in Items)
        {
            _vmIndex[vm.FilePath] = vm;
            _pathIndex.Add(vm.FilePath);
        }

        if (currentFilePath is not null && _vmIndex.TryGetValue(currentFilePath, out var newCurrentItem))
        {
            CurrentItem = newCurrentItem;
        }
        else
        {
            _currentItemIndex = -1;
            CurrentItem = null;
        }
    }

    private static List<string> EnumerateVideoFilesFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return [];

        var filePaths = new List<string>();

        try
        {
            foreach (var file in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                if (IsVideoFile(file))
                    filePaths.Add(file);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip folders we can't access.
        }
        catch (IOException)
        {
            // Skip transient file system errors.
        }

        return filePaths;
    }

    private static (List<string> FilePaths, bool HasUnsupported) CollectDroppedVideoFiles(IEnumerable<string> droppedPaths)
    {
        var filePaths = new List<string>();
        var hasUnsupported = false;

        foreach (var path in droppedPaths)
        {
            if (Directory.Exists(path))
            {
                filePaths.AddRange(EnumerateVideoFilesFromDirectory(path));
                continue;
            }

            if (!File.Exists(path)) continue;

            if (IsVideoFile(path))
                filePaths.Add(path);
            else
                hasUnsupported = true;
        }

        return (filePaths, hasUnsupported);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
