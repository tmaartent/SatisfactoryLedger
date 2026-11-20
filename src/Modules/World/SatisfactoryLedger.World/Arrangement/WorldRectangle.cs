namespace SatisfactoryLedger.World.Arrangement;

/// <summary>A rectangle in the world seen from above, in the game's centimetres, as the map places things.</summary>
internal sealed record WorldRectangle(double X0, double Y0, double X1, double Y1);
