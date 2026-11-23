namespace SatisfactoryLedger.Catalog.Browsing.Dto.Responses;

/// <summary>The game's icons there are (ADR-0032): the game version and each descriptor that has one.</summary>
internal sealed record IconsResponse(string GameVersion, IReadOnlyList<string> Descriptors);
