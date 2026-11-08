using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Factories;

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