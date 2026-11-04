using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Factories;

internal sealed record BuildableReference(string Id, string ClassName);

/// <param name="Items">The items on the belt: what the machines feeding it produce.</param>
/// <param name="Into">What the belt ends in: a storage container or consumer, or nothing.</param>
/// <param name="Machines">
/// Per building class, how many machines a factory with this output belt would contain, before any input items are
/// declared. It helps telling candidates apart.
/// </param>
internal sealed record OutputBeltSuggestion(
    string Belt,
    string ClassName,
    IReadOnlyList<string> Items,
    IReadOnlyList<BuildableReference> Into,
    PositionDocument? Position,
    IReadOnlyDictionary<string, int> Machines);

/// <summary>The candidate output belts of a snapshot (BR-W12), with what a player needs to pick one.</summary>
internal static class OutputBeltSuggestions
{
    public static IReadOnlyList<OutputBeltSuggestion> For(SnapshotDocument snapshot)
    {
        WorldGraph world = snapshot.ToWorldGraph();
        Dictionary<string, BuildableDocument> buildables = snapshot.Buildables.ToDictionary(buildable => buildable.Id, StringComparer.Ordinal);

        return
        [
            .. CandidateOutputBelts.In(world).Select(candidate =>
            {
                BuildableDocument belt = buildables[candidate.Belt.Id];
                FactoryMembers wouldContain = new FactoryDefinition("Suggestion", candidate.Belt, []).TraceMembers(world);

                return new OutputBeltSuggestion(
                    belt.Id,
                    belt.ClassName,
                    [.. candidate.Items.Select(item => item.Id).Order(StringComparer.Ordinal)],
                    [.. candidate.Into.Select(id => new BuildableReference(id.Id, buildables[id.Id].ClassName))],
                    belt.Position,
                    wouldContain.Machines
                        .GroupBy(id => buildables[id.Id].ClassName, StringComparer.Ordinal)
                        .OrderBy(group => group.Key, StringComparer.Ordinal)
                        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal));
            })
        ];
    }
}
