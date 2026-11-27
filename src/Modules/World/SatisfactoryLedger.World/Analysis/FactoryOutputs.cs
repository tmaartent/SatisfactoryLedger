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
                .. SnapshotAnalysis.Shown(output.Destinations, destination => destination.PerMinute)
                    .Select(shown => new OutputDestination(
                        shown.Value.Kind,
                        shown.Value.Owner,
                        shown.Value.Owner is { } owner ? names[owner] : null,
                        shown.Value.Building,
                        shown.PerMinute)),
            ])),
    ];
}
