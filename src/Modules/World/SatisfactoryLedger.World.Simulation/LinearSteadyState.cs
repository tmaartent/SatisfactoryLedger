using Google.OrTools.LinearSolver;

using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>
/// The steady state as one linear programme (ADR-0026), solved with OR-Tools' GLOP. Part B: without the sharing rules
/// of splitters and mergers, which the sequence of programmes adds.
/// <list type="bullet">
/// <item>Variables: the flow of each item over each connection it can reach (<see cref="ItemsOnConnections"/>), and a
/// utilisation between 0 and 1 per machine.</item>
/// <item>A machine takes <c>demand × u</c> of each input (at most that of a stocked one, BR-A17) and makes
/// <c>production × u</c> of each product (BR-A1).</item>
/// <item>Logistics passes on what comes in, per item; a belt, lift or pipe at most its capacity (BR-A3). A provider
/// delivers at most its offer (BR-A2). Sinks and sources of unknown rate take and give freely (BR-A7, A9).</item>
/// <item>Objective: every machine as fast as it can go, and every provider delivering as much as is taken. A tiny
/// penalty on every flow keeps items from circling without reason.</item>
/// </list>
/// </summary>
internal static class LinearSteadyState
{
    /// <summary>Against flows that circle in a loop of logistics for nothing: small enough never to outweigh real flow.</summary>
    private const double Circling = 1e-6;

    public static LinearFlow Solve(FlowNetwork network)
    {
        ArgumentNullException.ThrowIfNull(network);

        using Solver solver = Solver.CreateSolver("GLOP") ?? throw new InvalidOperationException("OR-Tools has no GLOP solver");
        List<int>[] into = Lists(network, edge => edge.To);
        List<int>[] outOf = Lists(network, edge => edge.From);
        IReadOnlyList<IReadOnlySet<ItemId>> items = ItemsOnConnections.Of(network);

        Objective objective = solver.Objective();
        objective.SetMaximization();
        Dictionary<(int Edge, ItemId Item), Variable> flow = [];
        for (int edge = 0; edge < network.Edges.Count; edge++)
        {
            foreach (ItemId item in items[edge])
            {
                Variable variable = solver.MakeNumVar(0, double.PositiveInfinity, $"f{edge}:{item}");
                objective.SetCoefficient(variable, -Circling);
                flow[(edge, item)] = variable;
            }
        }

        IEnumerable<Variable> Over(IEnumerable<int> edges, ItemId item) =>
            edges.Where(edge => flow.ContainsKey((edge, item))).Select(edge => flow[(edge, item)]);

        Dictionary<int, Variable> utilisation = [];
        for (int node = 0; node < network.Nodes.Count; node++)
        {
            switch (network.Nodes[node])
            {
                case FlowMachine { Demand.Count: 0, Production.Count: 0 }:
                    // No recipe the analysis knows: it asks and makes nothing (BR-W10).
                    break;
                case FlowMachine machine:
                    Variable u = solver.MakeNumVar(0, 1, $"u{node}");
                    objective.SetCoefficient(u, 1);
                    utilisation[node] = u;
                    foreach ((ItemId item, double demand) in machine.Demand.Where(demand => demand.Value > 0))
                    {
                        // A stocked input counts as supplied: it still takes what its belts bring, up to its demand.
                        bool stocked = machine.Stocked?.Contains(item) == true;
                        Constraint takes = solver.MakeConstraint(stocked ? double.NegativeInfinity : 0, 0);
                        Sum(takes, Over(into[node], item));
                        takes.SetCoefficient(u, -demand);
                    }

                    foreach ((ItemId item, double production) in machine.Production.Where(product => product.Value > 0))
                    {
                        // A product with no line of its form to leave by holds the machine at 0 (BR-A3).
                        Constraint makes = solver.MakeConstraint(0, 0);
                        Sum(makes, Over(outOf[node], item));
                        makes.SetCoefficient(u, -production);
                    }

                    break;
                case FlowConduit or FlowSplitter or FlowMerger:
                    HashSet<ItemId> passing = [.. into[node].Concat(outOf[node]).SelectMany(edge => items[edge])];
                    foreach (ItemId item in passing)
                    {
                        Constraint conserved = solver.MakeConstraint(0, 0);
                        Sum(conserved, Over(into[node], item));
                        Sum(conserved, Over(outOf[node], item), -1);
                    }

                    if (network.Nodes[node] is FlowConduit { Capacity: < double.PositiveInfinity } conduit)
                    {
                        Constraint capacity = solver.MakeConstraint(0, conduit.Capacity);
                        Sum(capacity, into[node].SelectMany(edge => items[edge].Select(item => flow[(edge, item)])));
                    }

                    break;
                case FlowProvider provider:
                    Constraint offer = solver.MakeConstraint(0, provider.Offer);
                    foreach (Variable delivered in Over(outOf[node], provider.Item))
                    {
                        offer.SetCoefficient(delivered, 1);
                        objective.SetCoefficient(delivered, 1 - Circling);
                    }

                    break;
            }
        }

        Solver.ResultStatus status = solver.Solve();
        if (status != Solver.ResultStatus.OPTIMAL)
        {
            throw new InvalidOperationException($"The linear programme has no optimal solution: {status}");
        }

        return new LinearFlow(
            network,
            [
                .. Enumerable.Range(0, network.Edges.Count).Select(edge => (IReadOnlyDictionary<ItemId, double>)items[edge]
                    .Select(item => (Item: item, Rate: flow[(edge, item)].SolutionValue()))
                    .Where(entry => entry.Rate > SteadyState.Tolerance)
                    .ToDictionary(entry => entry.Item, entry => entry.Rate))
            ],
            network.Nodes.Select((node, index) => (Node: node, Index: index))
                .Where(entry => entry.Node is FlowMachine)
                .ToDictionary(
                    entry => entry.Node.Id,
                    entry => utilisation.TryGetValue(entry.Index, out Variable? u) ? Math.Clamp(u.SolutionValue(), 0, 1) : 0));
    }

    private static void Sum(Constraint constraint, IEnumerable<Variable> variables, double coefficient = 1)
    {
        foreach (Variable variable in variables)
        {
            constraint.SetCoefficient(variable, coefficient);
        }
    }

    private static List<int>[] Lists(FlowNetwork network, Func<(int From, int To), int> end)
    {
        List<int>[] lists = [.. network.Nodes.Select(_ => new List<int>())];
        for (int edge = 0; edge < network.Edges.Count; edge++)
        {
            lists[end(network.Edges[edge])].Add(edge);
        }

        return lists;
    }
}
