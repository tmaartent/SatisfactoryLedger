using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Tracing;

namespace SatisfactoryLedger.World.Factories;

/// <summary>A stored factory matched in a snapshot (BR-W11): one of the <see cref="SnapshotFactories"/>.</summary>
internal sealed class TracedFactory
{
    private readonly Lazy<bool> isTraceable;
    private readonly Lazy<FactoryMembers> members;
    private readonly Lazy<IReadOnlyDictionary<BuildableId, Guid>> owners;

    /// <param name="definition">Its definition as matched; null when it has no machines.</param>
    /// <param name="world">The snapshot's world graph, built the first time one of the factories needs it.</param>
    /// <param name="owners">Who each machine belongs to world-wide: <see cref="SnapshotFactories.Owners"/>.</param>
    internal TracedFactory(
        StoredFactory factory,
        MachineMatch match,
        FactoryDefinition? definition,
        Lazy<WorldGraph> world,
        IReadOnlySet<ItemId> fluids,
        IReadOnlyDictionary<BuildableId, Guid> owners)
    {
        Factory = factory;
        Machines = match.Ids;
        Missing = match.Missing;
        isTraceable = new Lazy<bool>(() => definition is not null && definition.IsTraceableIn(world.Value));
        members = new Lazy<FactoryMembers>(() => !isTraceable.Value
            ? throw new InvalidOperationException($"Factory {factory.Id} can't be traced in this snapshot.")
            : definition!.TraceMembers(world.Value, fluids));
        this.owners = new Lazy<IReadOnlyDictionary<BuildableId, Guid>>(() => OwnersAsSeenBy(factory.Id, members.Value, owners));
    }

    public StoredFactory Factory { get; }

    /// <summary>
    /// Per stored machine, in their order, its id in the snapshot: its own, or the id of the machine that replaced it
    /// (BR-W11). A missing one keeps its own id, which the snapshot doesn't have. Matched only: nothing is traced.
    /// </summary>
    public IReadOnlyList<BuildableId> Machines { get; }

    /// <summary>Its stored machines with no match in the snapshot.</summary>
    public IReadOnlyList<StoredMachine> Missing { get; }

    /// <summary>Whether any of its machines is in the snapshot (BR-W11): checked the first time it's read.</summary>
    public bool IsTraceable => isTraceable.Value;

    /// <summary>
    /// What belongs to it in the snapshot (BR-W3, BR-W4): traced the first time it's read, then kept. Only for a
    /// traceable factory.
    /// </summary>
    public FactoryMembers Members => members.Value;

    /// <summary>
    /// Who each machine belongs to as this factory sees it: its own machines are its own, even one an older factory
    /// has too (BR-W5); the rest as <see cref="SnapshotFactories.Owners"/> says. Built the first time it's read, then
    /// kept. Only for a traceable factory.
    /// </summary>
    public IReadOnlyDictionary<BuildableId, Guid> Owners => owners.Value;

    private static Dictionary<BuildableId, Guid> OwnersAsSeenBy(Guid id, FactoryMembers members, IReadOnlyDictionary<BuildableId, Guid> owners)
    {
        Dictionary<BuildableId, Guid> seen = members.Machines.ToDictionary(machine => machine, _ => id);
        foreach ((BuildableId machine, Guid owner) in owners)
        {
            seen.TryAdd(machine, owner);
        }

        return seen;
    }
}
