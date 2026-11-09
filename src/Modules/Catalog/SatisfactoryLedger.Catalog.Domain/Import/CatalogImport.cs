using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.Domain.Import;

/// <summary>The record of one import of the game's Docs file (ADR-0005): which game version, when, and what changed.</summary>
public sealed class CatalogImport
{
    // EF Core can only pass scalar properties to a constructor; it sets the counts and the list afterwards.
    private CatalogImport(CatalogImportId id, CatalogImportSource source, string gameVersion, DateTimeOffset importedAt)
    {
        Id = id;
        Source = source;
        GameVersion = gameVersion;
        ImportedAt = importedAt;
    }

    public static CatalogImport RecordDocs(
        CatalogImportId id,
        string gameVersion,
        DateTimeOffset importedAt,
        CatalogEntryChanges<Item> items,
        CatalogEntryChanges<Building> buildings,
        CatalogEntryChanges<TransportTier> transportTiers,
        CatalogEntryChanges<Recipe> recipes,
        CatalogEntryChanges<Extractor> extractors,
        CatalogEntryChanges<Footprint> footprints,
        CatalogEntryChanges<Generator> generators,
        CatalogEntryChanges<DronePort> dronePorts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameVersion);

        List<string> noLongerInGame =
        [
            .. items.NoLongerInGame.Select(item => item.Id.Id),
            .. buildings.NoLongerInGame.Select(building => building.Id.Id),
            .. transportTiers.NoLongerInGame.Select(tier => tier.Id.Id),
            .. recipes.NoLongerInGame.Select(recipe => recipe.Id.Id),
            .. extractors.NoLongerInGame.Select(extractor => extractor.Id.Id),
            .. footprints.NoLongerInGame.Select(footprint => footprint.Id.Id),
            .. generators.NoLongerInGame.Select(generator => generator.Id.Id),
            .. dronePorts.NoLongerInGame.Select(port => port.Id.Id)
        ];

        return new CatalogImport(id, CatalogImportSource.DocsFile, gameVersion.Trim(), importedAt)
        {
            Items = items.Counts,
            Buildings = buildings.Counts,
            TransportTiers = transportTiers.Counts,
            Recipes = recipes.Counts,
            Extractors = extractors.Counts,
            Footprints = footprints.Counts,
            Generators = generators.Counts,
            DronePorts = dronePorts.Counts,
            // A building that left the game takes its footprint with it: one class name, listed once.
            NoLongerInGame = [.. noLongerInGame.Distinct(StringComparer.Ordinal)]
        };
    }

    /// <summary>An import of the node table: only resource nodes change (ADR-0015).</summary>
    public static CatalogImport RecordResourceNodes(
        CatalogImportId id, string gameVersion, DateTimeOffset importedAt, CatalogEntryChanges<ResourceNode> resourceNodes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameVersion);

        return new CatalogImport(id, CatalogImportSource.ResourceNodeTable, gameVersion.Trim(), importedAt)
        {
            ResourceNodes = resourceNodes.Counts,
            NoLongerInGame = [.. resourceNodes.NoLongerInGame.Select(node => node.Id.Id)]
        };
    }

    public CatalogImportId Id { get; }

    public CatalogImportSource Source { get; }

    public string GameVersion { get; }

    public DateTimeOffset ImportedAt { get; }

    public ImportCounts Items { get; private init; } = new(0, 0, 0);

    public ImportCounts Buildings { get; private init; } = new(0, 0, 0);

    public ImportCounts TransportTiers { get; private init; } = new(0, 0, 0);

    public ImportCounts Recipes { get; private init; } = new(0, 0, 0);

    public ImportCounts Extractors { get; private init; } = new(0, 0, 0);

    public ImportCounts Footprints { get; private init; } = new(0, 0, 0);

    public ImportCounts Generators { get; private init; } = new(0, 0, 0);

    public ImportCounts DronePorts { get; private init; } = new(0, 0, 0);

    public ImportCounts ResourceNodes { get; private init; } = new(0, 0, 0);

    /// <summary>Class names of the entries this import marked as no longer in the game.</summary>
    public IReadOnlyList<string> NoLongerInGame { get; private init; } = [];
}