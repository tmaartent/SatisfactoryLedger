namespace SatisfactoryLedger.World.Contracts.Arrangement;

/// <summary>A rectangle on the arrangement's grid: its lowest corner and its size, in cells.</summary>
public sealed record GridRectangleDocument(int X, int Y, int Width, int Depth);
