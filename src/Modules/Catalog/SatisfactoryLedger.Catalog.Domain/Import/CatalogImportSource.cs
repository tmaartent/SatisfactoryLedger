namespace SatisfactoryLedger.Catalog.Domain.Import;

/// <summary>What an import read: the game's Docs file, or what the map tool writes: the node table or the map art (ADR-0015).</summary>
public enum CatalogImportSource
{
    DocsFile,
    ResourceNodeTable,
    MapArt,
}