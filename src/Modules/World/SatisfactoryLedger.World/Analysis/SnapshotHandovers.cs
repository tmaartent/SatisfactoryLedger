using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Tracing;
using SatisfactoryLedger.World.Factories;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Analysis;

/// <summary>
/// A factory's <see cref="FactoryHandovers"/> in a snapshot: the facts the module can't derive yet (decision 3 of #50),
/// read from the snapshot and the factories matched in it. Classifying buildings at import is card 8.
/// </summary>
internal static class SnapshotHandovers
{
    /// <summary>The class of the AWESOME Sink: what reaches it is sunk.</summary>
    private const string SinkClass = "Build_ResourceSink_C";

    /// <summary>The consumers that use up what they get, shown by building (decided 2026-11-11). Stations and uploaders store it.</summary>
    private static readonly HashSet<string> UsesUp = new(["Build_Portal_C", "Build_AlienPowerBuilding_C"], StringComparer.Ordinal);

    /// <param name="factories">The world's factories matched in the snapshot, this one among them: who a machine at the other end belongs to.</param>
    public static FactoryHandovers Of(SnapshotDocument snapshot, SnapshotFactories factories, TracedFactory factory, FlowReadings readings) =>
        FactoryHandovers.Of(
            factories.World,
            readings,
            factory.Members,
            factory.Owners,
            snapshot.Buildables.Where(buildable => buildable.ClassName == SinkClass).Select(buildable => BuildableId.Of(buildable.Id)).ToHashSet(),
            snapshot.Buildables.Where(buildable => UsesUp.Contains(buildable.ClassName)).Select(buildable => BuildableId.Of(buildable.Id)).ToHashSet(),
            snapshot.Buildables.ToDictionary(buildable => BuildableId.Of(buildable.Id), buildable => buildable.ClassName));

    /// <summary>Each factory's name by its id, this one's too: the handovers name owners by id (decision 7 of #50).</summary>
    public static Dictionary<Guid, string> NamesOf(StoredFactory factory, IEnumerable<StoredFactory> factories) =>
        factories.Prepend(factory).DistinctBy(other => other.Id).ToDictionary(other => other.Id, other => other.Name);
}
