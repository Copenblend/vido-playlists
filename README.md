# Playlists Plugin for Vido

The Playlists plugin adds playlist creation, management, and playback support to Vido. Build playlists from your media library, reorder items with drag-and-drop, and let the plugin handle sequential or shuffled playback with automatic looping.

## Features

- Create, save, and load playlists in the `.vidpl` JSON format
- Add files via the explorer right-click **Add to Playlist** menu or drag-and-drop from Windows Explorer
- Drag-and-drop reordering within the playlist sidebar
- Sequential playback with looping (wraps from last to first and vice versa)
- Shuffle mode using a Fisher-Yates full-list shuffle — every item plays once before repeating
- Auto-save option that persists changes as you make them
- Recent playlists dropdown for quick access to your last 10 playlists
- Status bar showing playlist progress (e.g. "Playing 3 of 12 — My Playlist")
- Companion file support — other non-video files are kept in the playlist alongside videos, making them discoverable by other plugins during playback

## Installation

1. Open Vido and go to **Settings → Plugins**
2. Click **Install from Registry** and select the Playlists plugin
3. Restart Vido when prompted

For manual installation, download the plugin zip and extract it to:
```
%APPDATA%\Vido\plugins\com.vido.playlists\
```
Then restart Vido.

## Getting Started

1. Click the **Playlists** icon in the sidebar to open the playlist panel
2. Drag files from Windows Explorer into the panel, or right-click files in Vido's file explorer and choose **Add to Playlist**
3. Double-click any item to start playback — the plugin takes over next/previous navigation
4. Use Vido's transport controls or `PageDown` / `PageUp` shortcuts to advance through the playlist

## Sidebar Panel

The sidebar panel is the main interface for the plugin:

- **New** — Create a new empty playlist (prompts to save if the current playlist has unsaved changes)
- **Open** — Load a `.vidpl` file from disk
- **Save** — Save the current playlist (prompts for a location if never saved)
- **Recent** (▾) — Quick access to the last 10 opened or saved playlists
- **Shuffle** — Toggle shuffle mode on or off

The playlist items are displayed in a list. The currently playing item is highlighted. Missing files appear grayed out with a warning indicator.

### Item Context Menu

Right-click any playlist item for:

- **Remove from Playlist**
- **Move Up** / **Move Down**
- **Move to Top** / **Move to Bottom**

### Drag-and-Drop

- **From Windows Explorer**: Drop files or folders onto the playlist panel to add them. Folders are scanned recursively.
- **Within the playlist**: Drag items to reorder them. A visual insertion indicator shows the drop position.

## Playback

When you double-click a playlist item, the plugin registers itself as Vido's active playlist provider. While active:

- **Next** (`PageDown` or transport button) advances to the next video in the playlist
- **Previous** (`PageUp` or transport button) goes to the previous video
- **Media ended** automatically advances to the next video
- Playback **loops** — after the last video, it wraps to the first (and vice versa for previous)
- Non-video files are skipped during navigation but remain in the playlist for companion-file discovery

If you load a video from outside the playlist (e.g. double-click in the file explorer), playlist mode deactivates and Vido reverts to its built-in next/previous behavior.

### Shuffle

When shuffle is enabled, the playlist is randomized using the Fisher-Yates algorithm. The currently playing item is placed first in the shuffled order. Navigation follows the shuffled sequence, and the list is re-shuffled after a full pass. Disabling shuffle returns to the original order.

## File Format

Playlists are saved as `.vidpl` files in a simple JSON format:

```json
{
  "name": "My Playlist",
  "items": [
    { "filePath": "C:\\Videos\\video1.mp4" },
    { "filePath": "C:\\Videos\\video1.funscript" },
    { "filePath": "C:\\Videos\\video2.mkv" }
  ]
}
```

## Settings

Plugin settings are available in **Vido → Settings → Playlists**:

| Setting | Default | Description |
|---------|---------|-------------|
| Automatically Save Playlists | Off | When enabled, changes are saved immediately. If the playlist has never been saved, you are prompted to choose a location. |

## Status Bar

The status bar at the bottom of Vido shows playlist information:

- When not playing: **My Playlist — 12 items**
- When playing: **Playing 3 of 12 — My Playlist**

## Requirements

- **Vido** version 0.8.0 or later
- **Windows** (the plugin uses WPF for its UI)

## License

MIT
