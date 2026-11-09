using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Domain.Flow;

/// <summary>
/// The steady-state flow simulation (BR-A1 to BR-A10). It alternates two passes until nothing changes:
/// <list type="number">
/// <item><b>backward</b>: per connection, how much the receiving end can take (a belt's capacity, what a machine still
/// asks for, a merger's even share);</item>
/// <item><b>forward</b>: per connection, what the sending end sends (a provider's offer, a splitter's even split over
/// what its outputs take, a machine's output at its utilisation).</item>
/// </list>
/// Back-pressure comes from the backward pass, shortages from the forward pass. A loop just takes more sweeps.
/// Next to what flows, each connection carries what its sender <b>offers</b>: what it would send if the receiving end
/// took everything. That tells a starved machine (too little offered) from a blocked one (offered, but its output
/// can't leave), and keeps two inputs of one machine from waiting for each other.
/// A connection carries solids or fluids, as its belt or pipe does: solids and fluids are divided separately.
/// Next to what it accepts, a connection can accept <b>spare</b>: what it takes only when the others have what they
/// want, such as the fuel of a drone port or truck station. A sender serves spare last.
/// Computed in <c>double</c>: results are approximations to <see cref="Tolerance"/> (BR-A10, ADR-0004).
/// Each connected part of the network is simulated on its own: it settles at its own pace, and one that doesn't
/// settle within <see cref="MaxSweeps"/> is reported as approximate without holding up the rest.
/// </summary>
public static class SteadyState
{
    /// <summary>Results are meaningful to this many items per minute (BR-A10).</summary>
    public const double Tolerance = 0.0001;

    /// <summary>A source with an unknown rate is confirmed supplied when its stock lasts this long (BR-A9).</summary>
    public const double ConfirmedMinutes = 30;

    /// <summary>The most sweeps one part gets. A part that hasn't settled by then is approximate.</summary>
    public const int MaxSweeps = 2_000;

    /// <summary>After this many sweeps without settling, a part's acceptance is damped against flip-flopping.</summary>
    internal const int DampAfter = 50;

    /// <summary>
    /// BR-A11: from this utilisation a machine counts as running, with no limit. A shortfall of less than a tenth of a
    /// percent is noise: about what the sweeps leave when a part settles slowly.
    /// </summary>
    public const double RunningFrom = 0.999;

    /// <summary>For comparing doubles inside the solver: a difference smaller than this is no difference.</summary>
    internal const double Settle = 1e-9;

    /// <summary>Solids, then fluids: <c>true</c> means fluid.</summary>
    internal static readonly bool[] Forms = [false, true];

    public static FlowResult Of(FlowNetwork network, int maxSweeps = MaxSweeps)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSweeps);

        var flows = new IReadOnlyDictionary<ItemId, double>[network.Edges.Count];
        Dictionary<BuildableId, MachineState> machines = [];
        List<Bottleneck> bottlenecks = [];
        List<UnusedSupply> unused = [];
        List<UnknownSupply> unknown = [];
        List<FlowPart> parts = [];

        foreach ((List<int> nodes, List<int> edges) in PartsOf(network))
        {
            var part = new FlowNetwork(
                nodes.Select(node => network.Nodes[node]),
                edges.Select(edge => new Connection(
                    network.Nodes[network.Edges[edge].From].Id,
                    network.Nodes[network.Edges[edge].To].Id,
                    network.Ports[edge].From,
                    network.Ports[edge].To)),
                network.Fluids);
            PartResult result = new Solver(part).Run(maxSweeps);

            for (int k = 0; k < edges.Count; k++)
            {
                flows[edges[k]] = result.Flows[k];
            }

            foreach ((BuildableId id, MachineState state) in result.Machines)
            {
                machines[id] = state;
            }

            bottlenecks.AddRange(result.Bottlenecks);
            unused.AddRange(result.Unused);
            unknown.AddRange(result.Unknown);
            parts.Add(new FlowPart([.. nodes.Select(node => network.Nodes[node].Id)], result.Settled, result.Sweeps));
        }

        return new FlowResult(network, flows, machines, bottlenecks, unused, unknown, parts);
    }

    /// <summary>The connected parts of a network, ignoring direction: per part its nodes and its connections.</summary>
    private static List<(List<int> Nodes, List<int> Edges)> PartsOf(FlowNetwork network)
    {
        int[] root = [.. Enumerable.Range(0, network.Nodes.Count)];

        foreach ((int from, int to) in network.Edges)
        {
            root[Find(from)] = Find(to);
        }

        Dictionary<int, (List<int> Nodes, List<int> Edges)> parts = [];
        for (int node = 0; node < network.Nodes.Count; node++)
        {
            int part = Find(node);
            if (!parts.TryGetValue(part, out (List<int> Nodes, List<int> Edges) found))
            {
                found = ([], []);
                parts[part] = found;
            }

            found.Nodes.Add(node);
        }

        for (int edge = 0; edge < network.Edges.Count; edge++)
        {
            parts[Find(network.Edges[edge].From)].Edges.Add(edge);
        }

        return [.. parts.Values];

        int Find(int node)
        {
            while (root[node] != node)
            {
                root[node] = root[root[node]];
                node = root[node];
            }

            return node;
        }
    }
}
