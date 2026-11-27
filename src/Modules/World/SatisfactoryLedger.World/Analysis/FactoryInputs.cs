using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Tracing;
using SatisfactoryLedger.World.Factories;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Analysis;

/// <summary>
/// What a factory takes in per item (decided 2026-11-11), as the factory page shows it: the inputs side of its
/// <see cref="FactoryHandovers"/>, rounded, with the supplying factories by name.
/// </summary>
internal static class FactoryInputs
{
    /// <param name="factories">The world's factories, this one among them: who a supplying machine belongs to.</param>
    public static List<FactoryInput> Of(
        SnapshotDocument snapshot,
        WorldGraph world,
        StoredFactory factory,
        FactoryMembers members,
        FlowReadings readings,
        IEnumerable<StoredFactory> factories)
    {
        FactoryHandovers handovers = SnapshotHandovers.Of(snapshot, world, factory, members, readings, factories);
        Dictionary<Guid, string> names = SnapshotHandovers.NamesOf(factory, factories);

        return
        [
            .. handovers.Inputs.Select(input => new FactoryInput(
                input.Item.Id,
                SnapshotAnalysis.Round(input.Arriving),
                SnapshotAnalysis.Round(input.Need),
                SnapshotAnalysis.Round(input.Short()),
                [
                    .. input.Sources
                        .Where(source => SnapshotAnalysis.Round(source.PerMinute) > 0)
                        .Select(source => new InputSource(
                            source.Kind,
                            source.Owner,
                            source.Owner is { } owner ? names[owner] : null,
                            source.Building,
                            source.Id?.Id,
                            SnapshotAnalysis.Round(source.PerMinute),
                            source.Machines.Count > 0 ? [.. source.Machines.Select(machine => machine.Id)] : null)),
                ])),
        ];
    }
}
