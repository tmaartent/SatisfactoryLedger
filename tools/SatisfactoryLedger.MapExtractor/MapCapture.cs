namespace SatisfactoryLedger.MapExtractor;

/// <summary>
/// What the game says about its map picture: the capture actor that took it from above (where it stands, in
/// centimetres, and how wide a square it saw) and the textures it's cut into.
/// </summary>
internal sealed record MapCapture(double CentreX, double CentreY, double Width, IReadOnlyList<MapImage> Images);
