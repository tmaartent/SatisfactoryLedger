using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Tracing;

namespace SatisfactoryLedger.World.Factories;

/// <summary>A stored factory matched in a snapshot (BR-W11): one of the <see cref="SnapshotFactories"/>.</summary>
internal sealed class TracedFactory
{
    private readonly Lazy<bool> isTraceable;
    private readonly Lazy<FactoryMembers> members;

    /// <param name="definition">Its definition as matched; null when it has no machines.</param>
    /// <param name="world">The snapshot's world graph, built the first time one of the factories needs it.</param>
    internal TracedFactory(StoredFactory factory, MachineMatch match, FactoryDefinition? definition, Lazy<WorldGraph> world, IReadOnlySet<ItemId> fluids)
    {
        Factory = factory;
        Machines = match.Ids;
        Missing = match.Missing;
        isTraceable = new Lazy<bool>(() => definition is not null && definition.IsTraceableIn(world.Value));
        members = new Lazy<FactoryMembers>(() => !isTraceable.Value
            ? throw new InvalidOperationException($"Factory {factory.Id} can't be traced in this snapshot.")
            : definition!.TraceMembers(world.Value, fluids));
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
}
