using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Tracing;

namespace SatisfactoryLedger.World.Factories;

/// <summary>A stored factory matched in a snapshot (BR-W11): one of the <see cref="SnapshotFactories"/>.</summary>
internal sealed class TracedFactory
{
    private readonly Lazy<FactoryMembers> members;

    /// <param name="traceable">Its definition as matched, when it can be traced in the world; null when it can't.</param>
    internal TracedFactory(StoredFactory factory, IReadOnlyList<StoredMachine> missing, FactoryDefinition? traceable, WorldGraph world, IReadOnlySet<ItemId> fluids)
    {
        Factory = factory;
        Missing = missing;
        IsTraceable = traceable is not null;
        members = new Lazy<FactoryMembers>(() => traceable is null
            ? throw new InvalidOperationException($"Factory {factory.Id} can't be traced in this snapshot.")
            : traceable.TraceMembers(world, fluids));
    }

    public StoredFactory Factory { get; }

    /// <summary>Its stored machines with no match in the snapshot.</summary>
    public IReadOnlyList<StoredMachine> Missing { get; }

    /// <summary>Whether any of its machines is in the snapshot (BR-W11).</summary>
    public bool IsTraceable { get; }

    /// <summary>
    /// What belongs to it in the snapshot (BR-W3, BR-W4): traced the first time it's read, then kept. Only for a
    /// traceable factory.
    /// </summary>
    public FactoryMembers Members => members.Value;
}
