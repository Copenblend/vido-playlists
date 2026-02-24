using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PlaylistPlugin.Models;

namespace PlaylistPlugin.Services;

/// <summary>
/// Handles serialization and deserialization of <see cref="Playlist"/> objects
/// to and from <c>.vidpl</c> JSON playlist files.
/// </summary>
public sealed class PlaylistFileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Creates a new empty playlist with a default name.
    /// </summary>
    public Playlist CreateNew()
    {
        var playlist = new Playlist("Untitled Playlist");
        playlist.IsDirty = false;
        return playlist;
    }

    /// <summary>
    /// Serializes a <see cref="Playlist"/> to a JSON <c>.vidpl</c> file.
    /// </summary>
    /// <param name="playlist">The playlist to save.</param>
    /// <param name="filePath">The destination file path.</param>
    public async Task SaveAsync(Playlist playlist, string filePath)
    {
        ArgumentNullException.ThrowIfNull(playlist);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var dto = new PlaylistDto
        {
            Name = playlist.Name,
            Items = playlist.Items.Select(i => new PlaylistItemDto { FilePath = i.FilePath }).ToList()
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(filePath, json);

        playlist.FilePath = filePath;
        playlist.IsDirty = false;
    }

    /// <summary>
    /// Deserializes a <see cref="Playlist"/> from a JSON <c>.vidpl</c> file.
    /// Items whose files no longer exist are kept in the list (UI will flag them).
    /// </summary>
    /// <param name="filePath">The playlist file to load.</param>
    /// <returns>The deserialized playlist.</returns>
    /// <exception cref="FileNotFoundException">The playlist file does not exist.</exception>
    /// <exception cref="InvalidDataException">The file is not valid JSON or missing required fields.</exception>
    public async Task<Playlist> LoadAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Playlist file not found.", filePath);

        var json = await File.ReadAllTextAsync(filePath);

        PlaylistDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<PlaylistDto>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Failed to parse playlist file: {filePath}", ex);
        }

        if (dto is null)
            throw new InvalidDataException($"Playlist file is empty or invalid: {filePath}");

        var name = string.IsNullOrWhiteSpace(dto.Name) ? "Untitled Playlist" : dto.Name;
        var items = (dto.Items ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i.FilePath))
            .Select(i => new PlaylistItem(i.FilePath!));

        var playlist = new Playlist(name, items)
        {
            FilePath = filePath,
            IsDirty = false
        };

        return playlist;
    }

    // ── DTOs for JSON serialization ──

    internal sealed class PlaylistDto
    {
        public string? Name { get; set; }
        public List<PlaylistItemDto>? Items { get; set; }
    }

    internal sealed class PlaylistItemDto
    {
        public string? FilePath { get; set; }
    }
}
