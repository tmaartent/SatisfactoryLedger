using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>
/// A running state to start the sweeps from (ADR-0026): per part of the world, the most that can flow, then every
/// machine as fast as it can go (<see cref="LinearProgramme"/>). Loops and whole regions whose machines wait on each
/// other start running; the sweeps then settle the game's rules from there, and wind down what can't keep going.
/// </summary>
public static class RunningStart
{
    /// <summary>Per connection of <paramref name="network"/>, in its order: per item, what to start from as flowing.</summary>
    public static IReadOnlyList<IReadOnlyDictionary<ItemId, double>> Of(FlowNetwork network)
    {
        ArgumentNullException.ThrowIfNull(network);

        // Part by part: smaller programmes are better conditioned, and one that fails leaves the others their start.
        var start = new IReadOnlyDictionary<ItemId, double>[network.Edges.Count];
        foreach ((FlowNetwork part, IReadOnlyList<int> edges) in network.Parts())
        {
            IReadOnlyList<IReadOnlyDictionary<ItemId, double>> flows;
            try
            {
                using var programme = new LinearProgramme(part, ItemsOnConnections.Of(part));
                programme.Keep(programme.Push, programme.Maximise(programme.Push));
                programme.Maximise(programme.Utilisation);
                flows = programme.Flows();
            }
            catch (InvalidOperationException)
            {
                // No running start for this part: the sweeps start it from zero, with its loops primed, as before.
                flows = [.. edges.Select(_ => new Dictionary<ItemId, double>())];
            }

            for (int k = 0; k < edges.Count; k++)
            {
                start[edges[k]] = flows[k];
            }
        }

        return start;
    }
}
