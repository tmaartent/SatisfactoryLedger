namespace SatisfactoryLedger.Catalog.Domain.Import;

/// <summary>What an import read: the game's Docs file, or what the map tool writes: the node table, the map art or the icons (ADR-0015).</summary>
public enum CatalogImportSource
{
    DocsFile,
    ResourceNodeTable,
    MapArt,
    Icons,
}