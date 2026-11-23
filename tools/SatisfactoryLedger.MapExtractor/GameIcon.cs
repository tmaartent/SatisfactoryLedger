namespace SatisfactoryLedger.MapExtractor;

/// <summary>A descriptor's icon as read from the game: its class name and the image as PNG.</summary>
internal sealed record GameIcon(string ClassName, byte[] Png);
