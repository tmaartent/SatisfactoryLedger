using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Domain.Flow;

/// <summary>
/// Turns the world into a flow network: which kind of node each buildable is, what it carries, and its rates as
/// doubles from here on (ADR-0004, amended).
/// </summary>
public static class WorldFlow
{
    /// <param name="fluids">The items that are fluids (Catalog): they go over pipes, every other item over belts.</param>
    public static FlowNetwork From(WorldGraph world, IEnumerable<ItemId> fluids)
    {
        ArgumentNullException.ThrowIfNull(world);

        Dictionary<BuildableId, IReadOnlySet<ItemId>> cargo = CargoOfEmptyStations(world);
        return new FlowNetwork(world.Buildables.Select(buildable => NodeOf(buildable, world, cargo)), world.Connections, fluids);
    }

    /// <summary>
    /// BR-A9: an empty station carries what the stations its lines meet hold, all of it when they differ: freight
    /// platforms on a load balancer, of which only one has cargo yet (decided 2026-11-10). A station whose lines meet
    /// no station holding anything has none here, and supplies whatever is asked.
    /// </summary>
    private static Dictionary<BuildableId, IReadOnlySet<ItemId>> CargoOfEmptyStations(WorldGraph world)
    {
        List<Provider> stations = [.. world.Buildables.OfType<Provider>().Where(provider => provider is { IsStation: true, Offer: null })];
        Dictionary<BuildableId, HashSet<BuildableId>> lines = stations.ToDictionary(station => station.Id, station => LineOf(station.Id, world));
        Dictionary<BuildableId, IReadOnlySet<ItemId>> cargo = [];
        foreach (Provider empty in stations.Where(station => !station.Stock.Values.Any(count => count > 0)))
        {
            HashSet<ItemId> carried =
            [
                .. stations.Where(other => other.Id != empty.Id && lines[other.Id].Overlaps(lines[empty.Id]))
                    .SelectMany(other => other.Stock.Where(entry => entry.Value > 0).Select(entry => entry.Key)),
            ];
            if (carried.Count > 0)
            {
                cargo[empty.Id] = carried;
            }
        }

        return cargo;
    }

    /// <summary>The logistics a buildable's products pass through, up to the next machine, consumer or station.</summary>
    private static HashSet<BuildableId> LineOf(BuildableId id, WorldGraph world)
    {
        HashSet<BuildableId> line = [];
        Queue<BuildableId> queue = new([id]);
        while (queue.TryDequeue(out BuildableId? current))
        {
            foreach (Logistics logistics in world.Downstream(current).OfType<Logistics>().Where(logistics => line.Add(logistics.Id)))
            {
                queue.Enqueue(logistics.Id);
            }
        }

        return line;
    }

    private static FlowNode NodeOf(Buildable buildable, WorldGraph world, Dictionary<BuildableId, IReadOnlySet<ItemId>> cargo) => buildable switch
    {
        Machine machine => new FlowMachine(
            machine.Id, PerMinute(machine.Demand), PerMinute(machine.Production), Stocked(machine.Demand, machine.InputStock),
            machine.Leaving is { Count: > 0 } leaving ? leaving : null),
        Provider { Offer: { } offer } provider => new FlowProvider(provider.Id, offer.Item, offer.PerMinute.ToDouble()),
        // BR-A9: its rate isn't known (a station, an unknown node), so it supplies what is asked. A station also
        // takes everything loaded onto it.
        Provider provider => new FlowUnknownSource(
            provider.Id, Count(provider.Stock), Carries.Anything, provider.Burns, provider.BurnRate?.ToDouble(), provider.Inventory, cargo.GetValueOrDefault(provider.Id)),
        // BR-A8: a generator burning a known fuel takes what it burns and makes its waste, like a machine.
        Consumer { Generation: { } generation } generator => new FlowMachine(
            generator.Id, Demand(generation), Leaves(generation),
            Stocked(new Dictionary<ItemId, Rate> { [generation.Fuel] = generation.Burns }, generator.InputStock)),
        Consumer consumer => new FlowSink(consumer.Id, consumer.Burns, consumer.TakesLeftoversOnly, consumer.BurnRate?.ToDouble(), Evidenced(consumer.Inventory, consumer.Id, world)),
        Logistics { Kind: LogisticsKind.Splitter or LogisticsKind.PipeJunction or LogisticsKind.PipeNetwork } splitter =>
            new FlowSplitter(splitter.Id, CarriesOf(splitter), splitter.SortRules),
        Logistics { Kind: LogisticsKind.Merger } merger => new FlowMerger(merger.Id, CarriesOf(merger), merger.InputPriorities),
        Logistics { Kind: LogisticsKind.StorageContainer or LogisticsKind.FluidBuffer } container => Container(container, world),
        Logistics conduit => new FlowConduit(conduit.Id, conduit.Capacity?.ToDouble() ?? double.PositiveInfinity, CarriesOf(conduit)),
        _ => throw new ArgumentException($"{buildable} is not a kind of buildable the flow analysis knows", nameof(buildable)),
    };

    /// <summary>
    /// BR-A7: at the end of a line a container takes everything until it's full; inside a line it passes items through.
    /// BR-A9: a container nothing feeds supplies what is asked.
    /// </summary>
    private static FlowNode Container(Logistics container, WorldGraph world)
    {
        if (!world.Downstream(container.Id).Any())
        {
            return new FlowSink(container.Id, Inventory: Evidenced(container.Inventory, container.Id, world));
        }

        return world.Upstream(container.Id).Any()
            ? new FlowConduit(container.Id, double.PositiveInfinity, CarriesOf(container))
            : new FlowUnknownSource(container.Id, Count(container.Stock), CarriesOf(container));
    }

    /// <summary>
    /// BR-A7: the save overrules "full". An item a machine feeding it (through logistics) shows leaving still arrives,
    /// so something empties the inventory (a train, a truck, a drone) at least as fast as those machines make it, each
    /// machine's share of it when its line reaches several ends.
    /// </summary>
    private static Inventory? Evidenced(Inventory? inventory, BuildableId id, WorldGraph world)
    {
        if (inventory is null)
        {
            return null;
        }

        Dictionary<ItemId, double> arriving = [];
        foreach (Machine machine in FeedingMachines(id, world))
        {
            foreach ((ItemId item, Rate makes) in machine.Production.Where(product =>
                         inventory.StackSizes.GetValueOrDefault(product.Key) is > 0 and var stackSize && machine.ShowsLeaving(product.Key, stackSize)))
            {
                // Its line may reach several ends: what it sends is shared over them.
                arriving[item] = arriving.GetValueOrDefault(item) + makes.ToDouble() / Math.Max(1, EndsOf(machine.Id, world));
            }
        }

        return arriving.Count == 0 ? inventory : inventory with { Arriving = arriving };
    }

    /// <summary>
    /// How many ends the line of <paramref name="id"/> reaches through logistics: machines, consumers and stations, and
    /// containers at the end of a line.
    /// </summary>
    private static int EndsOf(BuildableId id, WorldGraph world)
    {
        int ends = 0;
        HashSet<BuildableId> visited = [id];
        Queue<BuildableId> queue = new([id]);
        while (queue.TryDequeue(out BuildableId? current))
        {
            foreach (Buildable downstream in world.Downstream(current).Where(downstream => visited.Add(downstream.Id)))
            {
                if (downstream is Logistics && world.Downstream(downstream.Id).Any())
                {
                    queue.Enqueue(downstream.Id);
                }
                else
                {
                    ends++;
                }
            }
        }

        return ends;
    }

    /// <summary>The machines whose products reach <paramref name="id"/> through logistics only.</summary>
    private static List<Machine> FeedingMachines(BuildableId id, WorldGraph world)
    {
        List<Machine> machines = [];
        HashSet<BuildableId> visited = [id];
        Queue<BuildableId> queue = new([id]);
        while (queue.TryDequeue(out BuildableId? current))
        {
            foreach (Buildable upstream in world.Upstream(current).Where(upstream => visited.Add(upstream.Id)))
            {
                switch (upstream)
                {
                    case Machine machine:
                        machines.Add(machine);
                        break;
                    case Logistics:
                        queue.Enqueue(upstream.Id);
                        break;
                }
            }
        }

        return machines;
    }

    private static Carries CarriesOf(Logistics logistics) =>
        logistics.Kind is LogisticsKind.Pipe or LogisticsKind.PipeJunction or LogisticsKind.PipeNetwork or LogisticsKind.FluidBuffer
            ? Carries.Fluids
            : Carries.Solids;

    /// <summary>BR-A17: the inputs whose stock lasts at least as long as a station's must to be confirmed (BR-A9).</summary>
    private static HashSet<ItemId> Stocked(IReadOnlyDictionary<ItemId, Rate> demand, IReadOnlyDictionary<ItemId, decimal> stock) =>
    [
        .. demand.Where(input => input.Value > Rate.Zero
                && stock.TryGetValue(input.Key, out decimal held)
                && (double)held >= input.Value.ToDouble() * SteadyState.ConfirmedMinutes)
            .Select(input => input.Key),
    ];

    private static Dictionary<ItemId, double> Count(IReadOnlyDictionary<ItemId, decimal> stock) =>
        stock.ToDictionary(entry => entry.Key, entry => (double)entry.Value);

    private static Dictionary<ItemId, double> PerMinute(IReadOnlyDictionary<ItemId, Rate> rates) =>
        rates.ToDictionary(rate => rate.Key, rate => rate.Value.ToDouble());

    private static Dictionary<ItemId, double> Demand(Generation generation)
    {
        Dictionary<ItemId, double> demand = new() { [generation.Fuel] = generation.Burns.ToDouble() };
        if (generation.Supplemental is { } supplemental && generation.SupplementalNeeded > Rate.Zero)
        {
            demand[supplemental] = generation.SupplementalNeeded.ToDouble();
        }

        return demand;
    }

    private static Dictionary<ItemId, double> Leaves(Generation generation) =>
        generation.Byproduct is { } byproduct && generation.Leaves > Rate.Zero ? new() { [byproduct] = generation.Leaves.ToDouble() } : [];
}
