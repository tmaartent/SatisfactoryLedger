namespace SatisfactoryLedger.Catalog.Domain.Map;

/// <summary>A rectangle of the world, in centimetres as the game's coordinates: x east, y south.</summary>
public sealed record MapArtBounds(double X0, double Y0, double X1, double Y1)
{
    /// <summary>
    /// The part of this rectangle a tile covers, cut in a grid of <paramref name="columns"/> by <paramref name="rows"/>
    /// equal tiles: columns along x (west to east), rows along y (north to south).
    /// </summary>
    public MapArtBounds Tile(int columns, int rows, int column, int row)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        double width = (X1 - X0) / columns;
        double height = (Y1 - Y0) / rows;
        return new MapArtBounds(X0 + (column * width), Y0 + (row * height), X0 + ((column + 1) * width), Y0 + ((row + 1) * height));
    }
}
