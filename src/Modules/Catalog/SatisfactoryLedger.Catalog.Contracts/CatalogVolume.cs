namespace SatisfactoryLedger.Catalog.Contracts;

/// <summary>
/// One clearance box of a building in 3D, in its own coordinates (centimetres): its corners in its own frame, turned by
/// the quaternion <c>Rotation*</c> and moved by <c>Offset*</c>.
/// </summary>
public sealed record CatalogVolume(
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
