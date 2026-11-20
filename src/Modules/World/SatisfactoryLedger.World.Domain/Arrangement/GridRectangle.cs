namespace SatisfactoryLedger.World.Domain.Arrangement;

/// <summary>A rectangle on the arrangement's grid, seen from above: its lowest corner and its size, in cells.</summary>
public sealed record GridRectangle
{
    public GridRectangle(int x, int y, int width, int depth)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(depth, 1);
        (X, Y, Width, Depth) = (x, y, width, depth);
    }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Depth { get; }

    /// <summary>Twice its centre, so that it's whole: x + x + width.</summary>
    public long DoubleCentreX => (2L * X) + Width;

    /// <summary>Twice its centre along y.</summary>
    public long DoubleCentreY => (2L * Y) + Depth;

    /// <summary>The same size, with its lowest corner elsewhere.</summary>
    public GridRectangle At(int x, int y) => new(x, y, Width, Depth);
}
