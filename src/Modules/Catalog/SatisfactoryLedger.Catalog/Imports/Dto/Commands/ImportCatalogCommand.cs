namespace SatisfactoryLedger.Catalog.Imports.Dto.Commands;

internal sealed record ImportCatalogCommand(Stream DocsFile, string GameVersion);