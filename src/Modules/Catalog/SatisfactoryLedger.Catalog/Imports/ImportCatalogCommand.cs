namespace SatisfactoryLedger.Catalog.Imports;

internal sealed record ImportCatalogCommand(Stream DocsFile, string GameVersion);