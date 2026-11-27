using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.Analysis;

/// <summary>
/// What a factory takes in per item (decided 2026-11-11), as the factory page shows it: the inputs side of its
/// <see cref="FactoryHandovers"/>, rounded, with the supplying factories by name.
/// </summary>
internal static class FactoryInputs
{
    /// <param name="names">Each factory's name by its id (<see cref="SnapshotHandovers.NamesOf"/>): who a source's owner is.</param>
    public static List<FactoryInput> Of(FactoryHandovers handovers, IReadOnlyDictionary<Guid, string> names) =>
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
                        SnapshotAnalysis.Round(source.PerMinute))),
            ])),
    ];
}
