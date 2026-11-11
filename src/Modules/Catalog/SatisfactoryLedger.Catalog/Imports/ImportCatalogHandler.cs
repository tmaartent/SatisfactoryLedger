using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.Catalog.Domain.Import;
using SatisfactoryLedger.Catalog.GameData.DocsFile;
using SatisfactoryLedger.Catalog.GameData.DocsFile.Dto;
using SatisfactoryLedger.Catalog.Persistence;
using SatisfactoryLedger.Catalog.GameData.Translator;
using SatisfactoryLedger.Catalog.Imports.Dto.Commands;
using SatisfactoryLedger.Catalog.Imports.Exceptions;

namespace SatisfactoryLedger.Catalog.Imports;

/// <summary>
/// Imports the game's Docs file into the catalog (ADR-0005): parse, translate into the domain, reconcile
/// with what is stored, save. Re-importing the same file changes nothing.
/// </summary>
internal sealed class ImportCatalogHandler(CatalogDbContext db, TimeProvider clock)
{
    public async Task<CatalogImport> HandleAsync(ImportCatalogCommand command, CancellationToken cancellationToken)
    {
        Domain.GameObjects.GameData fromGame = await ReadDocsFileAsync(command.DocsFile, cancellationToken);

        // Loaded entities are tracked: the changes Reconcile makes to them are saved by SaveChangesAsync.
        var items = CatalogEntries.Reconcile(await db.Items.ToListAsync(cancellationToken), fromGame.Items);
        var buildings = CatalogEntries.Reconcile(await db.Buildings.ToListAsync(cancellationToken), fromGame.Buildings);
        var transportTiers = CatalogEntries.Reconcile(await db.TransportTiers.ToListAsync(cancellationToken), fromGame.TransportTiers);
        var recipes = CatalogEntries.Reconcile(await db.Recipes.ToListAsync(cancellationToken), fromGame.Recipes);
        var extractors = CatalogEntries.Reconcile(await db.Extractors.ToListAsync(cancellationToken), fromGame.Extractors);
        var footprints = CatalogEntries.Reconcile(await db.Footprints.ToListAsync(cancellationToken), fromGame.Footprints);
        var generators = CatalogEntries.Reconcile(await db.Generators.ToListAsync(cancellationToken), fromGame.Generators);
        var dronePorts = CatalogEntries.Reconcile(await db.DronePorts.ToListAsync(cancellationToken), fromGame.DronePorts);
        var storages = CatalogEntries.Reconcile(await db.Storages.ToListAsync(cancellationToken), fromGame.Storages);
        var buildables = CatalogEntries.Reconcile(await db.Buildables.ToListAsync(cancellationToken), fromGame.Buildables);

        db.Items.AddRange(items.Added);
        db.Buildings.AddRange(buildings.Added);
        db.TransportTiers.AddRange(transportTiers.Added);
        db.Recipes.AddRange(recipes.Added);
        db.Extractors.AddRange(extractors.Added);
        db.Footprints.AddRange(footprints.Added);
        db.Generators.AddRange(generators.Added);
        db.DronePorts.AddRange(dronePorts.Added);
        db.Storages.AddRange(storages.Added);
        db.Buildables.AddRange(buildables.Added);

        var import = CatalogImport.RecordDocs(CatalogImportId.New(), command.GameVersion, clock.GetUtcNow(),
            items, buildings, transportTiers, recipes, extractors, footprints, generators, dronePorts, storages, buildables);
        db.Imports.Add(import);

        await db.SaveChangesAsync(cancellationToken);
        return import;
    }

    private static async Task<Domain.GameObjects.GameData> ReadDocsFileAsync(Stream docsFile, CancellationToken cancellationToken)
    {
        try
        {
            List<GameDataGroupDto> groups = await SatisfactoryImporter.Import(docsFile, cancellationToken);

            return new Domain.GameObjects.GameData(
                SatisfactoryTranslator.TranslateToItems(groups),
                SatisfactoryTranslator.TranslateToBuildings(groups),
                SatisfactoryTranslator.TranslateToTransportTiers(groups),
                SatisfactoryTranslator.TranslateToRecipes(groups),
                SatisfactoryTranslator.TranslateToExtractors(groups),
                SatisfactoryTranslator.TranslateToFootprints(groups),
                SatisfactoryTranslator.TranslateToGenerators(groups),
                SatisfactoryTranslator.TranslateToDronePorts(groups),
                SatisfactoryTranslator.TranslateToStorages(groups),
                SatisfactoryTranslator.TranslateToBuildables(groups));
        }
        catch (Exception e) when (e is JsonException or ArgumentException or KeyNotFoundException or FormatException)
        {
            throw new InvalidDocsFileException(e.Message, e);
        }
    }
}