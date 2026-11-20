using System.Text.Json;

using SatisfactoryLedger.Catalog.Domain.Map;

namespace SatisfactoryLedger.Catalog.GameData.MapArtFile;

/// <summary>
/// Reads the map art the map tool writes (ADR-0020). Our own format, version 1; anything else is refused with an
/// <see cref="InvalidDataException"/>, never half read. Tiles are served back as the type they say they are, so only
/// JPEG and PNG get in.
/// </summary>
internal static class MapArtReader
{
    private const int SupportedVersion = 1;

    private static readonly HashSet<string> ImageTypes = new(StringComparer.Ordinal) { "image/jpeg", "image/png" };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<MapArt> ReadAsync(Stream file, CancellationToken cancellationToken)
    {
        FileDto dto = await JsonSerializer.DeserializeAsync<FileDto>(file, Json, cancellationToken)
            ?? throw new InvalidDataException("The map art is empty");

        if (dto.Version != SupportedVersion)
        {
            throw new InvalidDataException($"Map art format version {dto.Version} is not supported, only {SupportedVersion}");
        }

        if (string.IsNullOrWhiteSpace(dto.GameVersion))
        {
            throw new InvalidDataException("The map art has no game version");
        }

        return MapArt.Create(dto.GameVersion, dto.X0, dto.Y0, dto.Width, dto.Height, [.. (dto.Tiles ?? []).Select(ToTile)]);
    }

    private static MapArtTile ToTile(TileDto tile)
    {
        if (tile.ContentType is null || !ImageTypes.Contains(tile.ContentType))
        {
            throw new InvalidDataException($"Tile {tile.Column}-{tile.Row} is {tile.ContentType ?? "of no type"}, not a JPEG or PNG image");
        }

        return new MapArtTile(tile.Column, tile.Row, tile.Image ?? [], tile.ContentType);
    }

    private sealed record FileDto(int Version, string? GameVersion, double X0, double Y0, double Width, double Height, List<TileDto>? Tiles);

    private sealed record TileDto(int Column, int Row, string? ContentType, byte[]? Image);
}
