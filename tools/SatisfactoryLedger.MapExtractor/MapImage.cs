namespace SatisfactoryLedger.MapExtractor;

/// <summary>One of the game's map textures, by its name (<c>Map_1-0</c>), encoded as JPEG.</summary>
internal sealed record MapImage(string Name, byte[] Jpeg);
