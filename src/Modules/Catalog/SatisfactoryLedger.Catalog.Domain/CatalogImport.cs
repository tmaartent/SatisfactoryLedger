namespace SatisfactoryLedger.Catalog.Domain;

public sealed record ImportCounts(int Added, int Updated, int NoLongerInGame);

public sealed record CatalogImportId
{
    private CatalogImportId(Guid id)
    {
        Id = id;
    }

    public Guid Id { get; }

    /// <summary>A version 7 GUID: it starts with a timestamp, so new ids sort after old ones.</summary>
    public static CatalogImportId New() => new(Guid.CreateVersion7());

    public static CatalogImportId Of(Guid id) => new(id);
}

/// <summary>The record of one import of the game's Docs file (ADR-0005): which game version, when, and what changed.</summary>
public sealed class CatalogImport
{
    // EF Core can only pass scalar properties to a constructor; it sets the counts and the list afterwards.
    private CatalogImport(CatalogImportId id, string gameVersion, DateTimeOffset importedAt)
    {
        Id = id;
        GameVersion = gameVersion;
        ImportedAt = importedAt;
    }

    public static CatalogImport Record(
        CatalogImportId id,
        string gameVersion,
        DateTimeOffset importedAt,
        CatalogEntryChanges<Item> items,
        CatalogEntryChanges<Building> buildings,
        CatalogEntryChanges<TransportTier> transportTiers,
        CatalogEntryChanges<Recipe> recipes,
        CatalogEntryChanges<Extractor> extractors)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameVersion);

        List<string> noLongerInGame =
        [
            .. items.NoLongerInGame.Select(item => item.Id.Id),
            .. buildings.NoLongerInGame.Select(building => building.Id.Id),
            .. transportTiers.NoLongerInGame.Select(tier => tier.Id.Id),
            .. recipes.NoLongerInGame.Select(recipe => recipe.Id.Id),
            .. extractors.NoLongerInGame.Select(extractor => extractor.Id.Id)
        ];

        return new CatalogImport(id, gameVersion.Trim(), importedAt)
        {
            Items = items.Counts,
            Buildings = buildings.Counts,
            TransportTiers = transportTiers.Counts,
            Recipes = recipes.Counts,
            Extractors = extractors.Counts,
            NoLongerInGame = noLongerInGame
        };
    }

    public CatalogImportId Id { get; }

    public string GameVersion { get; }

    public DateTimeOffset ImportedAt { get; }

    public ImportCounts Items { get; private init; } = new(0, 0, 0);

    public ImportCounts Buildings { get; private init; } = new(0, 0, 0);

    public ImportCounts TransportTiers { get; private init; } = new(0, 0, 0);

    public ImportCounts Recipes { get; private init; } = new(0, 0, 0);

    public ImportCounts Extractors { get; private init; } = new(0, 0, 0);

    /// <summary>Class names of the entries this import marked as no longer in the game.</summary>
    public IReadOnlyList<string> NoLongerInGame { get; private init; } = [];
}