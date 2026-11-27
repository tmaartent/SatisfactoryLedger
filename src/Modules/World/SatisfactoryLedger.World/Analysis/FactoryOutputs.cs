using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.Analysis;

/// <summary>
/// What a factory makes per item (BR-A14, BR-A18), as the factory page shows it: the outputs side of its
/// <see cref="FactoryHandovers"/>, rounded, with the receiving factories by name.
/// </summary>
internal static class FactoryOutputs
{
    /// <param name="names">Each factory's name by its id (<see cref="SnapshotHandovers.NamesOf"/>): who a destination's owner is.</param>
    public static List<FactoryOutput> Of(FactoryHandovers handovers, IReadOnlyDictionary<Guid, string> names) =>
    [
        .. handovers.Outputs.Select(output => new FactoryOutput(
            output.Item.Id,
            SnapshotAnalysis.Round(output.Now),
            SnapshotAnalysis.Round(output.AtFullSpeed),
            SnapshotAnalysis.Round(output.ReceiversNeed),
            SnapshotAnalysis.Round(output.OthersBring),
            SnapshotAnalysis.Round(output.Short),
            SnapshotAnalysis.Round(output.Sunk),
            SnapshotAnalysis.Round(output.CannotLeave),
            [
                .. output.Destinations
                    .Where(destination => SnapshotAnalysis.Round(destination.PerMinute) > 0)
                    .Select(destination => new OutputDestination(
                        destination.Kind,
                        destination.Owner,
                        destination.Owner is { } owner ? names[owner] : null,
                        destination.Building,
                        SnapshotAnalysis.Round(destination.PerMinute))),
            ])),
    ];
}
