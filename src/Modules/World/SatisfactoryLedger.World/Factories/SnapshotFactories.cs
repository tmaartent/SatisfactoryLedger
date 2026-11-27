using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Tracing;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Factories;

/// <summary>
/// Factories in a snapshot: every stored factory of a world matched in one of its snapshots, in one pass (BR-W11). Each
/// one's machines found or missing, whether it's traceable, its members, and who owns each machine (BR-W5).
/// </summary>
internal sealed class SnapshotFactories
{
    private readonly Lazy<WorldGraph> world;
    private readonly Dictionary<Guid, TracedFactory> byId;

    private SnapshotFactories(Lazy<WorldGraph> world, List<TracedFactory> factories, Dictionary<BuildableId, Guid> owners)
    {
        this.world = world;
        Factories = factories;
        Owners = owners;
        byId = [];
        foreach (TracedFactory factory in factories)
        {
            byId.TryAdd(factory.Factory.Id, factory);
        }
    }

    /// <summary>The snapshot's world graph: built the first time it's read, then kept.</summary>
    public WorldGraph World => world.Value;

    /// <summary>The factories in the order given.</summary>
    public IReadOnlyList<TracedFactory> Factories { get; }

    /// <summary>
    /// Who each machine belongs to, as matched in the snapshot (a missing one under its own id, which the snapshot
    /// doesn't have). Of the factories that have one machine, from before BR-W5, the first in the order given.
    /// </summary>
    public IReadOnlyDictionary<BuildableId, Guid> Owners { get; }

    /// <summary>
    /// Matches each factory's stored machines in the snapshot, in one pass. The world graph, which the world, whether a
    /// factory is traceable and its members need, is built once, the first time one of them is read.
    /// </summary>
    public static SnapshotFactories Of(SnapshotDocument snapshot, IEnumerable<StoredFactory> factories)
    {
        Lazy<WorldGraph> world = new(snapshot.ToWorldGraph);
        HashSet<ItemId> fluids = [.. snapshot.Fluids.Select(ItemId.Of)];
        List<TracedFactory> traced = [];
        Dictionary<BuildableId, Guid> owners = [];
        foreach (StoredFactory factory in factories)
        {
            MachineMatch match = MachineMatch.In(snapshot, factory.Machines);
            traced.Add(new TracedFactory(factory, match, factory.ToDefinition(match), world, fluids));
            foreach (BuildableId id in match.Ids)
            {
                owners.TryAdd(id, factory.Id);
            }
        }

        return new SnapshotFactories(world, traced, owners);
    }

    /// <summary>The factory with this id; null when it isn't one of them.</summary>
    public TracedFactory? Find(Guid id) => byId.GetValueOrDefault(id);
}
