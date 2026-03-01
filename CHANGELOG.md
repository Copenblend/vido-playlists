# Changelog

All notable changes to the Playlists Plugin for Vido will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-02-24

### Maintenance / Validation

- Included in vido-series cross-repo ticket validation runs (vido-113 through vido-118).
- Adopted strict completion gate expectation: zero build warnings and zero test warnings when repository is part of ticket validation scope.

### Added

- **Playlist management** — create, save, load, and manage playlists in the `.vidpl` JSON format
- **Sidebar panel** with playlist item list, toolbar buttons (New, Open, Save, Recent), and shuffle toggle
- **Add to Playlist** context menu item in the Vido file explorer for files and folders
- **External drag-and-drop** — drop files and folders from Windows Explorer onto the playlist panel; folders are scanned recursively
- **Internal drag-and-drop reordering** with visual insertion indicator and auto-scroll near edges
- **Item context menu** with Remove, Move Up, Move Down, Move to Top, and Move to Bottom actions
- **Sequential playback** via `IPlaylistProvider` — next/previous navigation integrates with Vido's transport controls and keyboard shortcuts (`PageDown` / `PageUp`)
- **Playlist looping** — playback wraps from last to first (and vice versa for previous)
- **Shuffle mode** using Fisher-Yates full-list shuffle with no repeats; re-shuffles after a complete pass; current item placed first in shuffled order
- **Auto-save setting** — when enabled, changes are saved immediately; prompts for a save location if the playlist has never been saved
- **Recent playlists dropdown** — quick access to the last 10 opened or saved playlists; stale entries removed automatically
- **Status bar** showing playlist progress ("Playing M of N — Playlist Name" or "Playlist Name — N items")
- **Companion file support** — non-video files (e.g. `.funscript`) are kept in the playlist for discovery by other plugins during playback
- **Missing file handling** — items whose files no longer exist are displayed grayed out with a warning indicator
- **Dark theme UI** matching Vido's palette
