namespace SatisfactoryLedger.World.Snapshots;

/// <summary>
/// One clearance box of a building in 3D, as the Catalog said at import: its corners in its own frame (centimetres),
/// turned by the quaternion <c>Rotation*</c> and moved by <c>Offset*</c> into the building's coordinates.
/// </summary>
internal sealed record VolumeDocument(
    decimal MinX,
    decimal MinY,
    decimal MinZ,
    decimal MaxX,
    decimal MaxY,
    decimal MaxZ,
    decimal RotationX,
    decimal RotationY,
    decimal RotationZ,
    decimal RotationW,
    decimal OffsetX,
    decimal OffsetY,
    decimal OffsetZ);
