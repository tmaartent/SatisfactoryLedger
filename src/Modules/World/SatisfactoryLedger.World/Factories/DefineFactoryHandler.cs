using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.Catalog.Contracts;
using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Factories;

/// <summary>
/// Defines a factory in a world (BR-W2, BR-W11). Its output belt is checked against the world's latest snapshot, its input
/// items against the Catalog.
/// </summary>
internal sealed class DefineFactoryHandler(WorldDbContext db, ICatalogQueries catalog)
{
    public async Task<DefineFactoryOutcome> HandleAsync(DefineFactoryCommand command, CancellationToken cancellationToken)
    {
        SnapshotDocument? latest = await db.LoadLatestOfWorldAsync(command.World, cancellationToken);
        if (latest is null)
        {
            return new DefineFactoryOutcome.UnknownWorld();
        }

        var factory = new StoredFactory
        {
            Id = Guid.CreateVersion7(),
            World = command.World,
            Name = command.Name.Trim(),
            OutputBelt = command.OutputBelt,
            InputItems = [.. command.InputItems.Distinct(StringComparer.Ordinal)],
        };

        if (!factory.ToDefinition().IsTraceableIn(latest.ToWorldGraph()))
        {
            return new DefineFactoryOutcome.NotAnOutputBelt();
        }

        IReadOnlyList<CatalogItem> known = await catalog.GetItemsAsync(factory.InputItems, cancellationToken);
        string[] unknown = [.. factory.InputItems.Except(known.Select(item => item.Id), StringComparer.Ordinal)];
        if (unknown.Length > 0)
        {
            return new DefineFactoryOutcome.UnknownInputItems(unknown);
        }

        if (await db.Factories.AnyAsync(existing => existing.World == factory.World && existing.Name == factory.Name, cancellationToken))
        {
            return new DefineFactoryOutcome.NameTaken();
        }

        db.Factories.Add(factory);
        await db.SaveChangesAsync(cancellationToken);
        return new DefineFactoryOutcome.Defined(factory);
    }
}