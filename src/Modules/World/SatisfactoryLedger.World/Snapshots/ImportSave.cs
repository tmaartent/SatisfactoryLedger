using SatisfactoryLedger.Catalog.Contracts;
using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.SaveFile;

namespace SatisfactoryLedger.World.Snapshots;

internal sealed record ImportSaveCommand(Stream Save);

/// <summary>
/// Imports a save as a new world snapshot (BR-W10, ADR-0016): read it through the adapter, copy the recipes it uses
/// from the Catalog, store it. What the Catalog doesn't know is listed, not refused.
/// </summary>
internal sealed class ImportSaveHandler(WorldDbContext db, ICatalogQueries catalog, TimeProvider clock)
{
    /// <exception cref="SaveFileException">The file can't be read as a save.</exception>
    public async Task<StoredSnapshot> HandleAsync(ImportSaveCommand command, CancellationToken cancellationToken)
    {
        SavedWorld saved = SaveFileReader.Read(command.Save);

        string[] recipeIds = [.. saved.Buildables.OfType<SavedMachine>().Select(machine => machine.Recipe).OfType<string>().Distinct()];
        IReadOnlyList<CatalogRecipe> recipes = await catalog.GetRecipesAsync(recipeIds, cancellationToken);
        SnapshotDocument document = SnapshotDocument.Create(saved, recipes);

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

        db.Snapshots.Add(snapshot);
        await db.SaveChangesAsync(cancellationToken);
        return snapshot;
    }
}
