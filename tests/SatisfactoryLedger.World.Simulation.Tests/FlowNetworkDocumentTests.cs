using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;

namespace SatisfactoryLedger.World.Simulation.Tests;

/// <summary>The flow network as the API sends it to the analysis service (ADR-0018), and back.</summary>
public class FlowNetworkDocumentTests
{
    private static readonly ItemId Ore = ItemId.Of("Desc_OreIron_C");
    private static readonly ItemId Ingot = ItemId.Of("Desc_IronIngot_C");
    private static readonly ItemId Water = ItemId.Of("Desc_Water_C");
    private static readonly ItemId Fuel = ItemId.Of("Desc_PackagedRocketFuel_C");

    private static BuildableId Id(string name) => BuildableId.Of(name);

    /// <summary>Every kind of node, with ports, rules, priorities, stock and fuel.</summary>
    private static FlowNetwork Network() => new(
        [
            new FlowProvider(Id("miner"), Ore, 60),
            new FlowProvider(Id("pump"), Water, 120),
            new FlowUnknownSource(Id("station"), new Dictionary<ItemId, double> { [Ore] = 1800 }, Carries.Solids, new HashSet<ItemId> { Fuel }),
            new FlowConduit(Id("ore"), 60, Carries.Solids),
            new FlowConduit(Id("container"), double.PositiveInfinity, Carries.Solids),
            new FlowConduit(Id("water"), 300, Carries.Fluids),
            new FlowMerger(Id("merge"), Carries.Solids, [2, 0]),
            new FlowSplitter(Id("split"), Carries.Solids, [new SortRule(0, SortRuleKind.Any), new SortRule(1, SortRuleKind.Overflow)]),
            new FlowMachine(Id("smelter"), new Dictionary<ItemId, double> { [Ore] = 30 }, new Dictionary<ItemId, double> { [Ingot] = 30 }),
            new FlowSink(Id("sink"), new HashSet<ItemId> { Fuel }),
            new FlowSink(Id("overflow")),
            new FlowSink(Id("tank")),
        ],
        [
            new Connection(Id("miner"), Id("ore")),
            new Connection(Id("ore"), Id("merge"), ToPort: 0),
            new Connection(Id("station"), Id("container")),
            new Connection(Id("container"), Id("merge"), ToPort: 1),
            new Connection(Id("merge"), Id("split")),
            new Connection(Id("split"), Id("smelter"), FromPort: 0),
            new Connection(Id("split"), Id("overflow"), FromPort: 1),
            new Connection(Id("smelter"), Id("sink")),
            new Connection(Id("pump"), Id("water")),
            new Connection(Id("water"), Id("tank")),
        ],
        [Water]);

    private static FlowNetwork RoundTrip(FlowNetwork network) => FlowNetworkDocument.FromJson(FlowNetworkDocument.From(network).ToJson()).ToNetwork();

    [Fact]
    public void A_flow_network_read_back_from_its_document_simulates_the_same()
    {
        FlowResult original = SteadyState.Of(Network());
        FlowResult readBack = SteadyState.Of(RoundTrip(Network()));

        Assert.Equal(original.Machine(Id("smelter")), readBack.Machine(Id("smelter")));
        Assert.Equal(original.Into(Id("overflow")), readBack.Into(Id("overflow")));
        Assert.Equal(original.UnusedSupply, readBack.UnusedSupply);
    }

    [Fact]
    public void A_belt_without_a_limit_keeps_having_none()
    {
        FlowNetwork network = RoundTrip(Network());

        Assert.Equal(double.PositiveInfinity, Assert.IsType<FlowConduit>(network.Nodes[network.IndexOf[Id("container")]]).Capacity);
    }

    [Fact]
    public void Ports_rules_priorities_stock_and_fuel_survive_the_document()
    {
        FlowNetwork network = RoundTrip(Network());
        FlowNode Node(string id) => network.Nodes[network.IndexOf[Id(id)]];

        Assert.Contains(network.Ports, port => port.From == 1);
        Assert.Contains(network.Ports, port => port.To == 1);
        Assert.Equal([2, 0], Assert.IsType<FlowMerger>(Node("merge")).Priorities!);
        Assert.Equal(SortRuleKind.Overflow, Assert.IsType<FlowSplitter>(Node("split")).Rules![1].Kind);
        var station = Assert.IsType<FlowUnknownSource>(Node("station"));
        Assert.Equal((1800.0, true), (station.Stock[Ore], station.Burns!.Contains(Fuel)));
        Assert.Equal([Water], network.Fluids);
    }

    [Fact]
    public void A_snapshot_imported_message_carries_only_the_snapshot_id()
    {
        var id = Guid.CreateVersion7();

        Assert.Equal(id, new SnapshotImported(id).SnapshotId);
    }
}
