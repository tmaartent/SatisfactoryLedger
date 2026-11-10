using Google.OrTools.LinearSolver;

using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>
/// The most that can flow through a part of the world, as a linear programme solved with OR-Tools' GLOP (ADR-0026).
/// It ignores how splitters and mergers share: that's the sweeps' job, started from this.
/// <list type="bullet">
/// <item>Variables: the flow of each item over each connection it can reach (<see cref="ItemsOnConnections"/>), and a
/// utilisation between 0 and 1 per machine.</item>
/// <item>A machine takes <c>demand × u</c> of each input (at most that of a stocked one, BR-A17) and makes
/// <c>production × u</c> of each product (BR-A1).</item>
/// <item>Logistics passes on what comes in, per item; a belt, lift or pipe at most its capacity (BR-A3). A provider
/// delivers at most its offer (BR-A2); a source of unknown rate at most what it's asked (BR-A9). A drone port whose
/// rate is known takes at most that of its fuel (BR-A16). Sinks take everything.</item>
/// </list>
/// Two objectives, the first held for the second: what the sources push in, then every machine's utilisation (which
/// runs loops no source pushes). Both have a tiny penalty on every flow, so items don't circle without reason.
/// </summary>
internal sealed class LinearProgramme : IDisposable
{
    /// <summary>Against flows that circle in a loop of logistics for nothing: small enough never to outweigh real flow.</summary>
    private const double Circling = 1e-6;

    private readonly FlowNetwork _network;
    private readonly ItemsOnConnections _items;
    private readonly Solver _solver;
    private readonly Dictionary<(int Edge, ItemId Item), Variable> _flow = [];
    private readonly List<(Variable Variable, double Coefficient)> _push = [];
    private readonly List<(Variable Variable, double Coefficient)> _utilisation = [];

    public LinearProgramme(FlowNetwork network, ItemsOnConnections items)
    {
        _network = network;
        _items = items;
        _solver = Solver.CreateSolver("GLOP") ?? throw new InvalidOperationException("OR-Tools has no GLOP solver");
        List<int>[] into = Lists(edge => edge.To);
        List<int>[] outOf = Lists(edge => edge.From);

        for (int edge = 0; edge < network.Edges.Count; edge++)
        {
            foreach (ItemId item in items.Items[edge])
            {
                _flow[(edge, item)] = _solver.MakeNumVar(0, double.PositiveInfinity, $"f{edge}:{item}");
            }
        }

        for (int node = 0; node < network.Nodes.Count; node++)
        {
            Build(node, into[node], outOf[node]);
        }
    }

    /// <summary>What the sources push in: provider deliveries, and what sources of unknown rate are asked for.</summary>
    public IReadOnlyList<(Variable Variable, double Coefficient)> Push => _push;

    /// <summary>Every machine's utilisation: what runs a loop no source pushes, such as one fed only by itself.</summary>
    public IReadOnlyList<(Variable Variable, double Coefficient)> Utilisation => _utilisation;

    /// <summary>Solves for the most of <paramref name="terms"/>, and returns how much that is.</summary>
    public double Maximise(IReadOnlyList<(Variable Variable, double Coefficient)> terms)
    {
        Objective objective = _solver.Objective();
        objective.Clear();
        foreach (Variable flow in _flow.Values)
        {
            objective.SetCoefficient(flow, -Circling);
        }

        foreach ((Variable variable, double coefficient) in terms)
        {
            objective.SetCoefficient(variable, coefficient + objective.GetCoefficient(variable));
        }

        objective.SetMaximization();
        Solver.ResultStatus status = _solver.Solve();
        return status == Solver.ResultStatus.OPTIMAL
            ? terms.Sum(term => term.Coefficient * term.Variable.SolutionValue())
            : throw new InvalidOperationException($"The linear programme has no optimal solution: {status}");
    }

    /// <summary>
    /// Holds <paramref name="terms"/> at the most just found, for the objective after it. A relative slack of 10⁻⁹:
    /// at 10⁻⁷, the penalty in the next objective spent it, and 20 came out as 19.999997.
    /// </summary>
    public void Keep(IReadOnlyList<(Variable Variable, double Coefficient)> terms, double value)
    {
        Constraint kept = _solver.MakeConstraint(value - (1e-9 * Math.Max(1, Math.Abs(value))), double.PositiveInfinity);
        foreach ((Variable variable, double coefficient) in terms)
        {
            kept.SetCoefficient(variable, coefficient + kept.GetCoefficient(variable));
        }
    }

    /// <summary>Per connection of the network, in its order: per item, what flows over it a minute in the last solution.</summary>
    public IReadOnlyList<IReadOnlyDictionary<ItemId, double>> Flows() =>
    [
        .. Enumerable.Range(0, _network.Edges.Count).Select(edge => (IReadOnlyDictionary<ItemId, double>)_items.Items[edge]
            .Select(item => (Item: item, Rate: _flow[(edge, item)].SolutionValue()))
            .Where(entry => entry.Rate > SteadyState.Tolerance)
            .ToDictionary(entry => entry.Item, entry => entry.Rate))
    ];

    public void Dispose() => _solver.Dispose();

    private void Build(int node, List<int> into, List<int> outOf)
    {
        switch (_network.Nodes[node])
        {
            case FlowMachine { Demand.Count: 0, Production.Count: 0 }:
                // No recipe the analysis knows: it asks and makes nothing (BR-W10).
                break;
            case FlowMachine machine:
                Variable u = _solver.MakeNumVar(0, 1, $"u{node}");
                _utilisation.Add((u, 1));
                foreach ((ItemId item, double demand) in machine.Demand.Where(demand => demand.Value > 0))
                {
                    // A stocked input counts as supplied: it still takes what its belts bring, up to its demand.
                    bool stocked = machine.Stocked?.Contains(item) == true;
                    Constraint takes = _solver.MakeConstraint(stocked ? double.NegativeInfinity : 0, 0);
                    Sum(takes, Over(into, item));
                    takes.SetCoefficient(u, -demand);
                }

                foreach ((ItemId item, double production) in machine.Production.Where(product => product.Value > 0))
                {
                    // A product with no line of its form to leave by holds the machine at 0 (BR-A3).
                    Constraint makes = _solver.MakeConstraint(0, 0);
                    Sum(makes, Over(outOf, item));
                    makes.SetCoefficient(u, -production);
                }

                break;
            case FlowConduit or FlowSplitter or FlowMerger:
                HashSet<ItemId> passing = [.. into.Concat(outOf).SelectMany(edge => _items.Items[edge])];
                foreach (ItemId item in passing)
                {
                    Constraint conserved = _solver.MakeConstraint(0, 0);
                    Sum(conserved, Over(into, item));
                    Sum(conserved, Over(outOf, item), -1);
                }

                if (_network.Nodes[node] is FlowConduit { Capacity: < double.PositiveInfinity } conduit)
                {
                    Constraint capacity = _solver.MakeConstraint(0, conduit.Capacity);
                    Sum(capacity, into.SelectMany(edge => _items.Items[edge].Select(item => _flow[(edge, item)])));
                }

                break;
            case FlowProvider provider:
                Constraint offer = _solver.MakeConstraint(0, provider.Offer);
                foreach (Variable delivered in Over(outOf, provider.Item))
                {
                    offer.SetCoefficient(delivered, 1);
                    _push.Add((delivered, 1));
                }

                break;
            case FlowSink { BurnRate: { } burnRate, Burns: { Count: > 0 } fuels }:
                // BR-A16: a drone port whose rate is known takes no more of its fuel than its drones burn.
                Burning(into, fuels, burnRate);
                break;
            case FlowUnknownSource source:
                if (source is { BurnRate: { } burns, Burns: { Count: > 0 } fuel })
                {
                    Burning(into, fuel, burns);
                }

                Constraint asked = _solver.MakeConstraint(0, _items.Asked.GetValueOrDefault(node));
                foreach (Variable delivered in outOf.SelectMany(edge => _items.Items[edge].Select(item => _flow[(edge, item)])))
                {
                    asked.SetCoefficient(delivered, 1);
                    _push.Add((delivered, 1));
                }

                break;
        }
    }

    private void Burning(List<int> into, IReadOnlySet<ItemId> fuels, double perMinute)
    {
        Constraint burns = _solver.MakeConstraint(0, perMinute);
        Sum(burns, fuels.SelectMany(fuel => Over(into, fuel)));
    }

    private IEnumerable<Variable> Over(IEnumerable<int> edges, ItemId item) =>
        edges.Where(edge => _flow.ContainsKey((edge, item))).Select(edge => _flow[(edge, item)]);

    private static void Sum(Constraint constraint, IEnumerable<Variable> variables, double coefficient = 1)
    {
        foreach (Variable variable in variables)
        {
            constraint.SetCoefficient(variable, coefficient);
        }
    }

    private List<int>[] Lists(Func<(int From, int To), int> end)
    {
        List<int>[] lists = [.. _network.Nodes.Select(_ => new List<int>())];
        for (int edge = 0; edge < _network.Edges.Count; edge++)
        {
            lists[end(_network.Edges[edge])].Add(edge);
        }

        return lists;
    }
}
