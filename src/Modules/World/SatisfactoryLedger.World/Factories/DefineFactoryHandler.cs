using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Factories;

/// <summary>
/// Defines a factory in a world by its machines (BR-W2, BR-W11), or edits one, checked against the world's latest
/// snapshot: none of them may be another factory's (BR-W5), and its name must be free in the world.
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
        DefineFactoryOutcome? invalid = WithMachines(factory, command.Machines, latest)
            ?? await ClashAsync(factory, latest, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        db.Factories.Add(factory);
        await db.SaveChangesAsync(cancellationToken);
        return new DefineFactoryOutcome.Defined(factory);
    }

    /// <summary>Gives a factory a new name and machines, with the same checks as defining it.</summary>
    public async Task<DefineFactoryOutcome> EditAsync(EditFactoryCommand command, CancellationToken cancellationToken)
    {
        StoredFactory? factory = await db.Factories.FindAsync([command.Id], cancellationToken);
        if (factory is null)
        {
            return new DefineFactoryOutcome.UnknownFactory();
        }

        SnapshotDocument? latest = await db.LoadLatestOfWorldAsync(factory.World, cancellationToken);
        if (latest is null)
        {
            return new DefineFactoryOutcome.UnknownWorld();
        }

        factory.Name = command.Name.Trim();
        DefineFactoryOutcome? invalid = WithMachines(factory, command.Machines, latest)
            ?? await ClashAsync(factory, latest, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new DefineFactoryOutcome.Defined(factory);
    }

    /// <summary>
    /// Another factory of the world with its name (BR-W11), or with any of its machines (BR-W5). Machines are matched
    /// across the world's factories as stored, this one's stored version included when it's edited, so a rebuilt
    /// machine goes to the same factory as on every other page.
    /// </summary>
    private async Task<DefineFactoryOutcome?> ClashAsync(StoredFactory factory, SnapshotDocument latest, CancellationToken cancellationToken)
    {
        // Untracked, so an edited factory comes back as stored rather than with its new name and machines.
        List<StoredFactory> stored = await db.Factories.AsNoTracking()
            .Where(existing => existing.World == factory.World)
            .ToListAsync(cancellationToken);
        if (stored.Any(existing => existing.Id != factory.Id && existing.Name == factory.Name))
        {
            return new DefineFactoryOutcome.NameTaken();
        }

        IReadOnlyList<TakenMachine> taken = ClaimedMachines.Among(factory.Machines.Select(machine => machine.Id), factory.Id, latest, stored);
        return taken.Count > 0 ? new DefineFactoryOutcome.MachinesTaken(taken) : null;
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
