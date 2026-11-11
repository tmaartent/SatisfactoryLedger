using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.Catalog.Contracts;
using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Factories;

/// <summary>
/// Defines a factory in a world by its machines (BR-W2, BR-W11), checked against the world's latest snapshot: none of
/// them may be another factory's (BR-W5). Until
/// the 3D selection replaces the old dialog, a factory can still be given by its output belt and input items: its
/// machines are then traced from that belt right away, and stored instead.
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

        var factory = new StoredFactory { Id = Guid.CreateVersion7(), World = command.World, Name = command.Name.Trim() };
        DefineFactoryOutcome? invalid = command.OutputBelt is null
            ? WithMachines(factory, command.Machines, latest)
            : await FromOutputBeltAsync(factory, command.OutputBelt, command.InputItems, latest, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        List<StoredFactory> others = await db.Factories.Where(existing => existing.World == factory.World).ToListAsync(cancellationToken);
        if (others.Any(existing => existing.Name == factory.Name))
        {
            return new DefineFactoryOutcome.NameTaken();
        }

        IReadOnlyList<TakenMachine> taken = ClaimedMachines.Among(factory.Machines.Select(machine => machine.Id), latest, others);
        if (taken.Count > 0)
        {
            return new DefineFactoryOutcome.MachinesTaken(taken);
        }

        db.Factories.Add(factory);
        await db.SaveChangesAsync(cancellationToken);
        return new DefineFactoryOutcome.Defined(factory);
    }

    private static DefineFactoryOutcome.NotMachines? WithMachines(StoredFactory factory, IEnumerable<string> ids, SnapshotDocument latest)
    {
        Dictionary<string, BuildableDocument> machines = latest.Buildables
            .Where(latest.IsMachineOrGenerator)
            .ToDictionary(buildable => buildable.Id, StringComparer.Ordinal);
        List<string> distinct = [.. ids.Distinct(StringComparer.Ordinal)];
        string[] notMachines = [.. distinct.Where(id => !machines.ContainsKey(id))];
        if (notMachines.Length > 0)
        {
            return new DefineFactoryOutcome.NotMachines(notMachines);
        }

        factory.Machines = [.. distinct.Order(StringComparer.Ordinal).Select(id => StoredMachine.From(machines[id]))];
        return null;
    }

    private async Task<DefineFactoryOutcome?> FromOutputBeltAsync(
        StoredFactory factory, string outputBelt, IEnumerable<string> inputItems, SnapshotDocument latest, CancellationToken cancellationToken)
    {
        List<string> items = [.. inputItems.Distinct(StringComparer.Ordinal)];
        IReadOnlyList<CatalogItem> known = await catalog.GetItemsAsync(items, cancellationToken);
        string[] unknown = [.. items.Except(known.Select(item => item.Id), StringComparer.Ordinal)];
        if (unknown.Length > 0)
        {
            return new DefineFactoryOutcome.UnknownInputItems(unknown);
        }

        factory.OutputBelt = outputBelt;
        factory.InputItems = items;
        return factory.ConvertIn(latest) ? null : new DefineFactoryOutcome.NotAnOutputBelt();
    }
}
