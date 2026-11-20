namespace SatisfactoryLedger.MapExtractor;

/// <summary>One tile of the map art: its column (west to east), its row (north to south) and its image.</summary>
internal sealed record MapArtTileEntry(int Column, int Row, string ContentType, byte[] Image);
