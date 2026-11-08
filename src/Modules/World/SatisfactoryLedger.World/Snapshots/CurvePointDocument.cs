using SatisfactoryLedger.World.SaveFile;

namespace SatisfactoryLedger.World.Snapshots;

/// <summary>A point of a belt's or pipe's curve, with the tangents it arrives and leaves with (world coordinates, centimetres).</summary>
internal sealed record CurvePointDocument(PositionDocument Location, PositionDocument ArriveTangent, PositionDocument LeaveTangent)
{
    /// <summary>Rounded to a hundredth of a centimetre: finer than anything the map shows, and a third of the size.</summary>
    public static CurvePointDocument From(SavedCurvePoint point) =>
        new(Rounded(point.Location), Rounded(point.ArriveTangent), Rounded(point.LeaveTangent));

    private static PositionDocument Rounded(SavedPosition position) =>
        new(Math.Round(position.X, 2), Math.Round(position.Y, 2), Math.Round(position.Z, 2));
}