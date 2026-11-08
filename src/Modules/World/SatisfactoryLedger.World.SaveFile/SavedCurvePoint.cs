namespace SatisfactoryLedger.World.SaveFile;

/// <summary>
/// A point of a belt's or pipe's curve (the game's <c>mSplineData</c>). Between two points the curve is a cubic Hermite
/// spline: it leaves one point along its <see cref="LeaveTangent"/> and arrives at the next along its
/// <see cref="ArriveTangent"/>. Locations in world coordinates (centimetres); tangents turned with the belt.
/// </summary>
public sealed record SavedCurvePoint(SavedPosition Location, SavedPosition ArriveTangent, SavedPosition LeaveTangent);