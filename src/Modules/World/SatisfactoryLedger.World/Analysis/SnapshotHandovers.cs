using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Tracing;
using SatisfactoryLedger.World.Factories;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Analysis;

/// <summary>
/// A factory's <see cref="FactoryHandovers"/> in a snapshot: the facts the module can't derive yet (decision 3 of #50),
/// read from the snapshot and the stored factories. Classifying buildings at import is card 8; matching factories
/// once is card 4.
/// </summary>
internal static class SnapshotHandovers
{
    /// <summary>The class of the AWESOME Sink: what reaches it is sunk.</summary>
    private const string SinkClass = "Build_ResourceSink_C";

    /// <summary>The consumers that use up what they get, shown by building (decided 2026-11-11). Stations and uploaders store it.</summary>
    private static readonly HashSet<string> UsesUp = new(["Build_Portal_C", "Build_AlienPowerBuilding_C"], StringComparer.Ordinal);

    /// <param name="factories">The world's factories, this one among them: who a machine at the other end belongs to.</param>
    public static FactoryHandovers Of(
        SnapshotDocument snapshot,
        WorldGraph world,
        StoredFactory factory,
        FactoryMembers members,
        FlowReadings readings,
        IEnumerable<StoredFactory> factories) => FactoryHandovers.Of(
            world,
            readings,
            members,
            OwnersIn(snapshot, factory, members, factories),
            snapshot.Buildables.Where(buildable => buildable.ClassName == SinkClass).Select(buildable => BuildableId.Of(buildable.Id)).ToHashSet(),
            snapshot.Buildables.Where(buildable => UsesUp.Contains(buildable.ClassName)).Select(buildable => BuildableId.Of(buildable.Id)).ToHashSet(),
            snapshot.Buildables.ToDictionary(buildable => BuildableId.Of(buildable.Id), buildable => buildable.ClassName));

    /// <summary>Each factory's name by its id, this one's too: the handovers name owners by id (decision 7 of #50).</summary>
    public static Dictionary<Guid, string> NamesOf(StoredFactory factory, IEnumerable<StoredFactory> factories) =>
        factories.Prepend(factory).DistinctBy(other => other.Id).ToDictionary(other => other.Id, other => other.Name);

    /// <summary>
    /// Who each machine in this snapshot belongs to: this factory's own members, and the other factories' machines as
    /// matched in it (BR-W11). The first factory wins for a machine stored in two, from before BR-W5.
    /// </summary>
    private static Dictionary<BuildableId, Guid> OwnersIn(
        SnapshotDocument snapshot, StoredFactory factory, FactoryMembers members, IEnumerable<StoredFactory> factories)
    {
        Dictionary<BuildableId, Guid> owners = members.Machines.ToDictionary(id => id, _ => factory.Id);
        foreach (StoredFactory other in factories.Where(other => other.Id != factory.Id))
        {
            foreach (BuildableId id in MachineMatch.In(snapshot, other.Machines).Ids)
            {
                owners.TryAdd(id, other.Id);
            }
        }

        return owners;
    }
}
