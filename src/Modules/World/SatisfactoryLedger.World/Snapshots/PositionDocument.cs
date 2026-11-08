using SatisfactoryLedger.World.SaveFile;

namespace SatisfactoryLedger.World.Snapshots;

/// <summary>Where a buildable stands, in the game's units (centimetres).</summary>
internal sealed record PositionDocument(double X, double Y, double Z)
{
    public static PositionDocument From(SavedPosition position) => new(position.X, position.Y, position.Z);
}