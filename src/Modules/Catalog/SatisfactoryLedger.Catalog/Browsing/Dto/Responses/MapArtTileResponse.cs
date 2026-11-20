namespace SatisfactoryLedger.Catalog.Browsing.Dto.Responses;

/// <summary>One tile of the map art: where it lies in the world, in centimetres, and where to get its image.</summary>
internal sealed record MapArtTileResponse(int Column, int Row, double X0, double Y0, double X1, double Y1, string Url);
