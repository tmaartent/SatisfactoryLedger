namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

/// <summary>
/// One clearance box in 3D, as the game's Docs file has it: its corners in its own frame (centimetres), turned by
/// <c>Rotation*</c> (a quaternion) and moved by <c>Offset*</c> into the building's coordinates. Draws a building in 3D.
/// </summary>
public sealed record ClearanceVolume(
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
