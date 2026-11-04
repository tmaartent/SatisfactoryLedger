using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.Catalog.Contracts;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Factories;

internal sealed record DefineFactoryCommand(string World, string Name, string OutputBelt, IReadOnlyList<string> InputItems);

/// <summary>What defining a factory can come to. A closed set: the endpoint handles each case.</summary>
internal abstract record DefineFactoryOutcome
{
    private DefineFactoryOutcome()
    {
    }

    internal sealed record Defined(StoredFactory Factory) : DefineFactoryOutcome;

    /// <summary>No snapshot has this session name, so there's no world to define it in.</summary>
    internal sealed record UnknownWorld : DefineFactoryOutcome;

    /// <summary>The output belt isn't a belt in the world's latest snapshot.</summary>
    internal sealed record NotAnOutputBelt : DefineFactoryOutcome;

    /// <summary>Input items the Catalog doesn't know: a typo, most likely (BR-W2).</summary>
    internal sealed record UnknownInputItems(IReadOnlyList<string> Items) : DefineFactoryOutcome;

    /// <summary>The world already has a factory with this name (BR-W11).</summary>
    internal sealed record NameTaken : DefineFactoryOutcome;
}

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
