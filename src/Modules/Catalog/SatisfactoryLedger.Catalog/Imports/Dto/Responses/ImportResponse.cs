using SatisfactoryLedger.Catalog.Domain.Import;

namespace SatisfactoryLedger.Catalog.Imports.Dto.Responses;

internal sealed record ImportResponse(
    Guid Id,
    CatalogImportSource Source,
    string GameVersion,
    DateTimeOffset ImportedAt,
    ImportCountsResponse Items,
    ImportCountsResponse Buildings,
    ImportCountsResponse TransportTiers,
    ImportCountsResponse Recipes,
    ImportCountsResponse Extractors,
    ImportCountsResponse Footprints,
    ImportCountsResponse Generators,
    ImportCountsResponse DronePorts,
    ImportCountsResponse ResourceNodes,
    IReadOnlyList<string> NoLongerInGame)
{
    public static ImportResponse From(CatalogImport import) => new(
        import.Id.Id,
        import.Source,
        import.GameVersion,
        import.ImportedAt,
        ToResponse(import.Items),
        ToResponse(import.Buildings),
        ToResponse(import.TransportTiers),
        ToResponse(import.Recipes),
        ToResponse(import.Extractors),
        ToResponse(import.Footprints),
        ToResponse(import.Generators),
        ToResponse(import.DronePorts),
        ToResponse(import.ResourceNodes),
        import.NoLongerInGame);

    private static ImportCountsResponse ToResponse(ImportCounts counts) =>
        new(counts.Added, counts.Updated, counts.NoLongerInGame);
}