using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Factories;

/// <summary>
/// Defines a factory in a world by its machines (BR-W2, BR-W11), checked against the world's latest snapshot: none of
/// them may be another factory's (BR-W5).
/// </summary>
internal sealed class DefineFactoryHandler(WorldDbContext db)
{
    public async Task<DefineFactoryOutcome> HandleAsync(DefineFactoryCommand command, CancellationToken cancellationToken)
    {
        SnapshotDocument? latest = await db.LoadLatestOfWorldAsync(command.World, cancellationToken);
        if (latest is null)
        {
            return new DefineFactoryOutcome.UnknownWorld();
        }

        var factory = new StoredFactory { Id = Guid.CreateVersion7(), World = command.World, Name = command.Name.Trim() };
        DefineFactoryOutcome? invalid = WithMachines(factory, command.Machines, latest);
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
}
