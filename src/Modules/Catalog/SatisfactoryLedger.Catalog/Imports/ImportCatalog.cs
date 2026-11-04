using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.Catalog.Domain;
using SatisfactoryLedger.Catalog.Importer;
using SatisfactoryLedger.Catalog.Persistence;
using SatisfactoryLedger.Catalog.Translator;

namespace SatisfactoryLedger.Catalog.Imports;

internal sealed record ImportCatalogCommand(Stream DocsFile, string GameVersion);

/// <summary>The uploaded file is not a Docs file the importer can read, or its data breaks a catalog rule.</summary>
internal sealed class InvalidDocsFileException(string message, Exception innerException) : Exception(message, innerException);

/// <summary>
/// Imports the game's Docs file into the catalog (ADR-0005): parse, translate into the domain, reconcile
/// with what is stored, save. Re-importing the same file changes nothing.
/// </summary>
internal sealed class ImportCatalogHandler(CatalogDbContext db, TimeProvider clock)
{
    public async Task<CatalogImport> HandleAsync(ImportCatalogCommand command, CancellationToken cancellationToken)
    {
        GameData fromGame = await ReadDocsFileAsync(command.DocsFile, cancellationToken);

        // Loaded entities are tracked: the changes Reconcile makes to them are saved by SaveChangesAsync.
        var items = CatalogEntries.Reconcile(await db.Items.ToListAsync(cancellationToken), fromGame.Items);
        var buildings = CatalogEntries.Reconcile(await db.Buildings.ToListAsync(cancellationToken), fromGame.Buildings);
        var transportTiers = CatalogEntries.Reconcile(await db.TransportTiers.ToListAsync(cancellationToken), fromGame.TransportTiers);
        var recipes = CatalogEntries.Reconcile(await db.Recipes.ToListAsync(cancellationToken), fromGame.Recipes);

        db.Items.AddRange(items.Added);
        db.Buildings.AddRange(buildings.Added);
        db.TransportTiers.AddRange(transportTiers.Added);
        db.Recipes.AddRange(recipes.Added);

        var import = CatalogImport.Record(CatalogImportId.New(), command.GameVersion, clock.GetUtcNow(),
            items, buildings, transportTiers, recipes);
        db.Imports.Add(import);

        await db.SaveChangesAsync(cancellationToken);
        return import;
    }

    private sealed record GameData(List<Item> Items, List<Building> Buildings, List<TransportTier> TransportTiers, List<Recipe> Recipes);

    private static async Task<GameData> ReadDocsFileAsync(Stream docsFile, CancellationToken cancellationToken)
    {
        try
        {
            List<GameDataGroupDto> groups = await SatisfactoryImporter.Import(docsFile, cancellationToken);

            return new GameData(
                SatisfactoryTranslator.TranslateToItems(groups),
                SatisfactoryTranslator.TranslateToBuildings(groups),
                SatisfactoryTranslator.TranslateToTransportTiers(groups),
                SatisfactoryTranslator.TranslateToRecipes(groups));
        }
        catch (Exception e) when (e is JsonException or ArgumentException or KeyNotFoundException or FormatException)
        {
            throw new InvalidDocsFileException(e.Message, e);
        }
    }
}