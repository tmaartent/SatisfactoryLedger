namespace SatisfactoryLedger.Catalog.Domain.Map;

/// <summary>
/// The game's map, the picture the player sees in the game (decided 2026-11-20, ADR-0020 amended): game data like the
/// rest of the Catalog, from the player's own install through the map tool (ADR-0015). It covers one rectangle of the
/// world, cut in a grid of tiles. There is one map art at a time: an import replaces it whole.
/// </summary>
public sealed class MapArt
{
    // EF Core binds only scalar constructor parameters; it sets the tiles afterwards.
    private MapArt(string gameVersion, double x0, double y0, double width, double height)
    {
        GameVersion = gameVersion;
        X0 = x0;
        Y0 = y0;
        Width = width;
        Height = height;
    }

    public static MapArt Create(string gameVersion, double x0, double y0, double width, double height, IReadOnlyList<MapArtTile> tiles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(tiles);
        if (tiles.Any(tile => tile.Image.Length == 0))
        {
            throw new ArgumentException("Every tile of the map art needs an image.", nameof(tiles));
        }

        // A full grid: every column and row once, so the tiles together cover the whole rectangle.
        int columns = tiles.Count == 0 ? 0 : tiles.Max(tile => tile.Column) + 1;
        int rows = tiles.Count == 0 ? 0 : tiles.Max(tile => tile.Row) + 1;
        bool full = tiles.Count > 0
            && tiles.All(tile => tile.Column >= 0 && tile.Row >= 0)
            && tiles.Select(tile => (tile.Column, tile.Row)).Distinct().Count() == tiles.Count
            && tiles.Count == columns * rows;
        if (!full)
        {
            throw new ArgumentException("The map art's tiles must form a full grid, each tile once.", nameof(tiles));
        }

        return new MapArt(gameVersion.Trim(), x0, y0, width, height) { Tiles = tiles };
    }

    public string GameVersion { get; }

    /// <summary>The west edge, in centimetres.</summary>
    public double X0 { get; }

    /// <summary>The north edge, in centimetres (the game's y grows southwards).</summary>
    public double Y0 { get; }

    public double Width { get; }

    public double Height { get; }

    public IReadOnlyList<MapArtTile> Tiles { get; private init; } = [];

    public MapArtBounds Bounds => new(X0, Y0, X0 + Width, Y0 + Height);
}
