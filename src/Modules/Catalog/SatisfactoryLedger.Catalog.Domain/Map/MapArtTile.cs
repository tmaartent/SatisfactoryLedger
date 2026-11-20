namespace SatisfactoryLedger.Catalog.Domain.Map;

/// <summary>
/// One image of the map art, at its column (west to east) and row (north to south). The game cuts its map in tiles
/// (<c>Map_0-1</c> is column 0, row 1).
/// </summary>
/// <param name="Image">The image's bytes, as <paramref name="ContentType"/> says.</param>
public sealed record MapArtTile(int Column, int Row, byte[] Image, string ContentType);
