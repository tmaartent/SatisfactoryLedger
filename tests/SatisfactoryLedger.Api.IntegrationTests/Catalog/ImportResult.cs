namespace SatisfactoryLedger.Api.IntegrationTests.Catalog;

public sealed record ImportResult(
    Guid Id,
    string Source,
    string GameVersion,
    DateTimeOffset ImportedAt,
    ImportCounts Items,
    ImportCounts Buildings,
    ImportCounts TransportTiers,
    ImportCounts Recipes,
    ImportCounts Extractors,
    ImportCounts ResourceNodes,
    string[] NoLongerInGame);