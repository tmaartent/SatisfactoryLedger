namespace SatisfactoryLedger.Catalog.Domain.Import;

/// <summary>What one import does to one kind of catalog entry.</summary>
public sealed record CatalogEntryChanges<TEntry>(
    IReadOnlyList<TEntry> Added,
    IReadOnlyList<TEntry> Updated,
    IReadOnlyList<TEntry> NoLongerInGame)
{
    public ImportCounts Counts => new(Added.Count, Updated.Count, NoLongerInGame.Count);
}