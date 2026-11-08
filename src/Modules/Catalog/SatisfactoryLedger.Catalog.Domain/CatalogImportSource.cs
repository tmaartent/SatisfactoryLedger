namespace SatisfactoryLedger.Catalog.Domain;

/// <summary>What an import read: the game's Docs file, or the node table the map tool writes (ADR-0015).</summary>
public enum CatalogImportSource
{
    DocsFile,
    ResourceNodeTable,
}