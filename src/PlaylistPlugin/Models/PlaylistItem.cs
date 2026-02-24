using System.IO;

namespace PlaylistPlugin.Models;

/// <summary>
/// Represents a single item in a playlist.
/// Each item references a file on disk (video, funscript, or any other type).
/// </summary>
public sealed class PlaylistItem : IEquatable<PlaylistItem>
{
    /// <summary>
    /// Absolute path to the file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Display name derived from <see cref="FilePath"/>.
    /// </summary>
    public string FileName { get; }

    public PlaylistItem(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
    }

    /// <summary>
    /// Equality based on <see cref="FilePath"/> (case-insensitive on Windows).
    /// </summary>
    public bool Equals(PlaylistItem? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(FilePath, other.FilePath, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => Equals(obj as PlaylistItem);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(FilePath);

    public override string ToString() => FileName;
}
