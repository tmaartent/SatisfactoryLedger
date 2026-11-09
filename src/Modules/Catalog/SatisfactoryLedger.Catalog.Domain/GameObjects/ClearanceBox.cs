namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

/// <summary>
/// One of the boxes the game keeps clear for a building (<c>mClearanceData</c>), seen from above: in the building's own
/// coordinates, in centimetres, already moved by its offset. A soft box is room around the building that other things
/// may share, such as the space a belt passes through a splitter.
/// </summary>
public sealed record ClearanceBox(decimal MinX, decimal MinY, decimal MaxX, decimal MaxY, bool Soft)
{
    /// <summary>The same box in 3D, as the Docs file has it; null when it wasn't read.</summary>
    public ClearanceVolume? Volume { get; init; }
}