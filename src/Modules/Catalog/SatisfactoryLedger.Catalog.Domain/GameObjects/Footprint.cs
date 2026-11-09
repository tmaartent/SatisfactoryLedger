using SatisfactoryLedger.Catalog.Domain.Import;

namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

/// <summary>
/// The ground a building covers, seen from above (ADR-0020): a rectangle in its own coordinates, in centimetres. The map
/// turns it with the building. Read from the game's Docs file, like the other catalog entries.
/// </summary>
public class Footprint : ICatalogEntry<Footprint>
{
    // EF Core stores this field (it needs a mutable List<T>); the domain only hands out a read-only view.
    private List<ClearanceVolume> _volumes = [];

    private Footprint(FootprintId id, decimal minX, decimal minY, decimal maxX, decimal maxY)
    {
        Id = id;
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    public static Footprint Create(FootprintId id, decimal minX, decimal minY, decimal maxX, decimal maxY)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxX, minX);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxY, minY);

        return new Footprint(id, minX, minY, maxX, maxY);
    }

    /// <summary>
    /// The rectangle around a building's boxes. Soft boxes count only when it has nothing else: the room around a
    /// building isn't the building.
    /// </summary>
    public static Footprint Around(FootprintId id, IReadOnlyCollection<ClearanceBox> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        if (boxes.Count == 0)
        {
            throw new ArgumentException($"{id.Id} has no clearance boxes to take a footprint from", nameof(boxes));
        }

        List<ClearanceBox> solid = [.. boxes.Where(box => !box.Soft)];
        IReadOnlyCollection<ClearanceBox> covered = solid.Count > 0 ? solid : boxes;
        Footprint footprint = Create(id, covered.Min(box => box.MinX), covered.Min(box => box.MinY), covered.Max(box => box.MaxX), covered.Max(box => box.MaxY));
        footprint._volumes = [.. covered.Select(box => box.Volume).OfType<ClearanceVolume>()];
        return footprint;
    }

    public FootprintId Id { get; }

    /// <summary>The boxes it covers, in 3D: the same ones as the rectangle, to draw the building in 3D.</summary>
    public IReadOnlyList<ClearanceVolume> Volumes => _volumes;

    public decimal MinX { get; private set; }

    public decimal MinY { get; private set; }

    public decimal MaxX { get; private set; }

    public decimal MaxY { get; private set; }

    /// <summary>Along the building's own x axis, in centimetres.</summary>
    public decimal Width => MaxX - MinX;

    /// <summary>Along the building's own y axis, in centimetres.</summary>
    public decimal Length => MaxY - MinY;

    public bool IsInGame { get; private set; } = true;

    public void UpdateFromGame(Footprint fromGame)
    {
        if (!Equals(fromGame))
        {
            throw new ArgumentException($"{fromGame} is the footprint of a different building than {this}", nameof(fromGame));
        }

        (MinX, MinY, MaxX, MaxY) = (fromGame.MinX, fromGame.MinY, fromGame.MaxX, fromGame.MaxY);
        _volumes = [.. fromGame._volumes];
        IsInGame = true;
    }

    public void MarkNoLongerInGame()
    {
        IsInGame = false;
    }

    public override bool Equals(object? obj) => obj is Footprint other && Id.Equals(other.Id);

    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => Id.Id;
}