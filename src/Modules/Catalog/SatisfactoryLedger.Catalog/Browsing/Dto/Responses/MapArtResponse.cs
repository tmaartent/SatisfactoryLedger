namespace SatisfactoryLedger.Catalog.Browsing.Dto.Responses;

/// <summary>The game's map picture (ADR-0020): the rectangle of the world it covers, in centimetres, and its tiles.</summary>
internal sealed record MapArtResponse(string GameVersion, double X0, double Y0, double X1, double Y1, IReadOnlyList<MapArtTileResponse> Tiles);
