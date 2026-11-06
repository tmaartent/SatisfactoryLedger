using SatisfactoryLedger.Catalog.Contracts;
using SatisfactoryLedger.World.Analysis;
using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.SaveFile;
using SatisfactoryLedger.World.Simulation;

using Wolverine.EntityFrameworkCore;

namespace SatisfactoryLedger.World.Snapshots;

internal sealed record ImportSaveCommand(Stream Save);

/// <summary>
/// Imports a save as a new world snapshot (BR-W10, ADR-0016): read it through the adapter, copy the recipes,
/// extractors, belts and nodes it uses from the Catalog, store it. What the Catalog doesn't know is listed, not refused.
/// </summary>
internal sealed class ImportSaveHandler(IDbContextOutbox<WorldDbContext> outbox, ICatalogQueries catalog, TimeProvider clock)
{
    /// <exception cref="SaveFileException">The file can't be read as a save.</exception>
    public async Task<StoredSnapshot> HandleAsync(ImportSaveCommand command, CancellationToken cancellationToken)
    {
        SavedWorld saved = SaveFileReader.Read(command.Save);

        SnapshotDocument document = SnapshotDocument.Create(saved, await CopyCatalogAsync(saved, cancellationToken));

        var snapshot = new StoredSnapshot
        {
            Id = Guid.CreateVersion7(),
            SessionName = saved.Info.SessionName,
            GameBuild = saved.Info.GameBuild,
            SavedAt = new DateTimeOffset(DateTime.SpecifyKind(saved.Info.SavedAtUtc, DateTimeKind.Utc)),
            ImportedAt = clock.GetUtcNow(),
            Counts = document.Counts(),
            UnknownRecipes = [.. document.UnknownRecipes],
            Unsupported = [.. document.Unsupported],
            Content = document.ToJson(),
        };

        // ADR-0018: the snapshot, its pending analysis and the message to the analysis service, in one transaction.
        // Wolverine sends the message to RabbitMQ once the transaction is committed (the transactional outbox).
        outbox.DbContext.Snapshots.Add(snapshot);
        outbox.DbContext.Analyses.Add(StoredAnalysis.PendingFor(snapshot.Id));
        await outbox.PublishAsync(new SnapshotImported(snapshot.Id));
        await outbox.SaveChangesAndFlushMessagesAsync(cancellationToken);
        return snapshot;
    }

    /// <summary>What the Catalog says about the recipes, extractors, belts, nodes and items this save uses.</summary>
    private async Task<CatalogCopy> CopyCatalogAsync(SavedWorld saved, CancellationToken cancellationToken)
    {
        string[] recipes = [.. saved.Buildables.OfType<SavedMachine>().Select(machine => machine.Recipe).OfType<string>().Distinct()];
        string[] extractors = [.. saved.Buildables.OfType<SavedProvider>().Select(provider => provider.ClassName).Distinct()];
        string[] tiers = [.. saved.Buildables.OfType<SavedLogistics>().Select(logistics => logistics.ClassName).Distinct()];
        string[] nodes = [.. saved.Buildables.OfType<SavedProvider>().Select(provider => provider.Node).OfType<string>().Distinct()];

        IReadOnlyList<CatalogRecipe> recipesKnown = await catalog.GetRecipesAsync(recipes, cancellationToken);
        IReadOnlyList<CatalogResourceNode> nodesKnown = await catalog.GetResourceNodesAsync(nodes, cancellationToken);
        HashSet<string> items = SnapshotDocument.ItemsUsed(
            saved,
            recipesKnown.Select(RecipeDocument.From),
            nodesKnown.Select(node => new ResourceNodeDocument(node.Name, node.Resource, node.Purity)));

        return new CatalogCopy(
            recipesKnown,
            await catalog.GetExtractorsAsync(extractors, cancellationToken),
            await catalog.GetTransportTiersAsync(tiers, cancellationToken),
            nodesKnown,
            await catalog.GetItemsAsync(items, cancellationToken));
    }
}
