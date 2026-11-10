using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>
/// The findings of one solved part (BR-A11 to A13, A9): why each machine runs below 100%, the bottlenecks, unused and
/// unknown supply. Read from the solution, and from what-ifs on the programme for what the solution alone can't say:
/// what a machine is offered, and what its outputs could take.
/// </summary>
internal sealed class LinearFindings(FlowNetwork network, LinearProgramme programme, LinearFlow solved)
{
    /// <summary>For comparing doubles: a difference smaller than this is no difference.</summary>
    private const double Settle = 1e-9;

    /// <summary>Solids, then fluids: <c>true</c> means fluid.</summary>
    private static readonly bool[] Forms = [false, true];

    private readonly List<int>[] _into = Lists(network, edge => edge.To);
    private readonly List<int>[] _outOf = Lists(network, edge => edge.From);

    /// <summary>The machines with a recipe that run below 99.9%: only these need what-ifs to say why.</summary>
    public static IEnumerable<int> Held(FlowNetwork network, LinearFlow solved) =>
        Enumerable.Range(0, network.Nodes.Count).Where(node => network.Nodes[node] is FlowMachine machine
            && (machine.Demand.Count > 0 || machine.Production.Count > 0)
            && solved.Utilisation[machine.Id] < SteadyState.RunningFrom);

    public IEnumerable<MachineState> Machines(IEnumerable<int> nodes) =>
        nodes.Select(node => StateOf(node, (FlowMachine)network.Nodes[node]));

    /// <summary>
    /// BR-A1, A11: a machine below 99.9% is output blocked when its outputs can take less than it's offered, and
    /// otherwise starved of its scarcest input. Every input offered less than its demand, and every product that can't
    /// all leave, is named with its share.
    /// </summary>
    private MachineState StateOf(int node, FlowMachine machine)
    {
        if (machine.Demand.Count == 0 && machine.Production.Count == 0)
        {
            return new MachineState(machine.Id, 0, MachineLimit.NoRecipe, null);
        }

        double utilisation = Math.Min(1, solved.Utilisation[machine.Id]);
        if (utilisation >= SteadyState.RunningFrom)
        {
            return new MachineState(machine.Id, utilisation, MachineLimit.None, null, new Dictionary<ItemId, double>(), new Dictionary<ItemId, double>());
        }

        Dictionary<ItemId, double> offered = machine.Demand.Where(demand => demand.Value > 0).ToDictionary(
            demand => demand.Key,
            demand => machine.Stocked?.Contains(demand.Key) == true ? 1 : Math.Min(1, programme.OfferedShare(node, demand.Key) ?? utilisation));

        Dictionary<ItemId, double> room = [];
        foreach (bool fluid in Forms)
        {
            HashSet<ItemId> products = [.. machine.Production.Where(product => product.Value > 0 && network.Fluids.Contains(product.Key) == fluid).Select(product => product.Key)];
            if (products.Count > 0)
            {
                double share = Math.Min(1, programme.OutputRoom(node, products) ?? utilisation);
                products.ToList().ForEach(product => room[product] = share);
            }
        }

        KeyValuePair<ItemId, double>? scarcest = offered.Count == 0 ? null : offered.MinBy(entry => entry.Value);
        KeyValuePair<ItemId, double>? tightest = room.Count == 0 ? null : room.MinBy(entry => entry.Value);
        (MachineLimit limit, ItemId? item) = (tightest?.Value ?? 1) < (scarcest?.Value ?? 1) - Settle
            ? (MachineLimit.OutputBlocked, tightest?.Key ?? machine.Production.Keys.First())
            : (MachineLimit.Starved, scarcest?.Key);

        Dictionary<ItemId, double> shortOf = offered.Where(entry => entry.Value < SteadyState.RunningFrom).ToDictionary(entry => entry.Key, entry => entry.Value);
        // Offered in full but not arriving (its sender is held back itself): what arrives of the item it's starved of.
        if (limit == MachineLimit.Starved && shortOf.Count == 0 && item is { } starvedOf && machine.Demand.TryGetValue(starvedOf, out double needed))
        {
            shortOf[starvedOf] = Math.Min(1, Into(node, starvedOf) / needed);
        }

        return new MachineState(machine.Id, utilisation, limit, item, shortOf,
            room.Where(entry => entry.Value < SteadyState.RunningFrom).ToDictionary(entry => entry.Key, entry => entry.Value));
    }

    /// <summary>
    /// BR-A12: a belt, lift or pipe at its capacity while more is offered upstream. Only the first saturated segment of a
    /// chain counts: a line of ten Mk.1 segments is one bottleneck.
    /// </summary>
    public List<Bottleneck> Bottlenecks()
    {
        double everything = network.Nodes.Sum(node => node switch
        {
            FlowProvider provider => provider.Offer,
            FlowConduit { Capacity: < double.PositiveInfinity } conduit => conduit.Capacity,
            _ => 0,
        });

        List<Bottleneck> bottlenecks = [];
        for (int node = 0; node < network.Nodes.Count; node++)
        {
            if (network.Nodes[node] is not FlowConduit { Capacity: < double.PositiveInfinity } conduit || !Saturated(node, conduit)
                || _into[node].Any(edge => network.Nodes[network.Edges[edge].From] is FlowConduit upstream && Saturated(network.Edges[edge].From, upstream)))
            {
                continue;
            }

            double flowing = Into(node);
            double offeredMore = (programme.OfferedInto(node, conduit.Capacity + everything) ?? flowing) - flowing;
            if (offeredMore > SteadyState.Tolerance)
            {
                bottlenecks.Add(new Bottleneck(conduit.Id, conduit.Capacity, flowing, offeredMore));
            }
        }

        return bottlenecks;
    }

    /// <summary>BR-A13: a provider delivering less than it offers.</summary>
    public List<UnusedSupply> UnusedSupply() =>
    [
        .. Enumerable.Range(0, network.Nodes.Count)
            .Where(node => network.Nodes[node] is FlowProvider)
            .Select(node => (Provider: (FlowProvider)network.Nodes[node], Delivered: _outOf[node].Sum(edge => solved.Flows[edge].Values.Sum())))
            .Where(supply => supply.Provider.Offer - supply.Delivered > SteadyState.Tolerance)
            .Select(supply => new UnusedSupply(supply.Provider.Id, supply.Provider.Item, supply.Provider.Offer, supply.Delivered))
    ];

    /// <summary>BR-A9: what each source of unknown rate delivers, and whether its stock lasts 30 minutes at that rate.</summary>
    public List<UnknownSupply> UnknownSupply()
    {
        List<UnknownSupply> unknown = [];
        for (int node = 0; node < network.Nodes.Count; node++)
        {
            if (network.Nodes[node] is not FlowUnknownSource source)
            {
                continue;
            }

            Dictionary<ItemId, double> delivered = _outOf[node]
                .SelectMany(edge => solved.Flows[edge])
                .Where(entry => entry.Value > SteadyState.Tolerance)
                .GroupBy(entry => entry.Key)
                .ToDictionary(item => item.Key, item => item.Sum(entry => entry.Value));
            if (delivered.Count == 0)
            {
                continue;
            }

            double? minutes = delivered.Keys.All(source.Stock.ContainsKey) ? delivered.Min(entry => source.Stock[entry.Key] / entry.Value) : null;
            unknown.Add(new UnknownSupply(source.Id, delivered, minutes, minutes >= SteadyState.ConfirmedMinutes - Settle));
        }

        return unknown;
    }

    private bool Saturated(int node, FlowConduit conduit) => Into(node) >= conduit.Capacity - SteadyState.Tolerance;

    private double Into(int node) => _into[node].Sum(edge => solved.Flows[edge].Values.Sum());

    private double Into(int node, ItemId item) => _into[node].Sum(edge => solved.Flows[edge].GetValueOrDefault(item));

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
