using SatisfactoryLedger.World.Domain.Flow;

namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>Which kind of flow node each buildable of the world becomes, with its rates (BR-A1 to BR-A9).</summary>
public class WorldFlowTests
{
    private static readonly ItemId Water = ItemId.Of("Desc_Water_C");

    private static FlowNode Node(TestWorld world, string id)
    {
        FlowNetwork network = WorldFlow.From(world.Build(), [Water]);
        return network.Nodes[network.IndexOf[BuildableId.Of(id)]];
    }

    [Fact]
    public void A_machine_asks_and_makes_its_rates_per_minute()
    {
        TestWorld world = new TestWorld().Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30);

        var smelter = Assert.IsType<FlowMachine>(Node(world, "smelter"));

        Assert.Equal(30, smelter.Demand[Items.IronOre]);
        Assert.Equal(30, smelter.Production[Items.IronIngot]);
    }

    [Fact]
    public void A_provider_with_a_known_rate_offers_it()
    {
        TestWorld world = new TestWorld().Miner("miner", Items.IronOre, 120);

        var miner = Assert.IsType<FlowProvider>(Node(world, "miner"));

        Assert.Equal((Items.IronOre, 120.0), (miner.Item, miner.Offer));
    }

    [Fact]
    public void A_provider_whose_rate_isnt_known_supplies_what_is_asked()
    {
        TestWorld world = new TestWorld().Miner("miner");

        Assert.IsType<FlowUnknownSource>(Node(world, "miner"));
    }

    [Fact]
    public void A_belt_carries_at_most_its_capacity()
    {
        TestWorld world = new TestWorld().Belt("belt", capacity: 270);

        Assert.Equal(270, Assert.IsType<FlowConduit>(Node(world, "belt")).Capacity);
    }

    [Fact]
    public void A_belt_whose_capacity_isnt_known_has_no_limit()
    {
        TestWorld world = new TestWorld().Belt("belt");

        Assert.Equal(double.PositiveInfinity, Assert.IsType<FlowConduit>(Node(world, "belt")).Capacity);
    }

    [Fact]
    public void A_consumer_takes_everything()
    {
        TestWorld world = new TestWorld().Sink("sink");

        Assert.IsType<FlowSink>(Node(world, "sink"));
    }

    [Fact]
    public void A_container_at_the_end_of_a_line_takes_everything()
    {
        TestWorld world = new TestWorld().Belt("belt").Container("box").Connect("belt", "box");

        Assert.IsType<FlowSink>(Node(world, "box"));
    }

    [Fact]
    public void A_container_inside_a_line_passes_items_through()
    {
        TestWorld world = new TestWorld().Belt("in").Container("box").Belt("out").Connect("in", "box", "out");

        Assert.Equal(double.PositiveInfinity, Assert.IsType<FlowConduit>(Node(world, "box")).Capacity);
    }

    [Fact]
    public void A_container_nothing_feeds_supplies_what_is_asked()
    {
        TestWorld world = new TestWorld().Container("box").Belt("out").Connect("box", "out");

        Assert.IsType<FlowUnknownSource>(Node(world, "box"));
    }

    [Fact]
    public void A_pipe_junction_splits_and_merges_like_a_splitter()
    {
        TestWorld world = new TestWorld().Junction("junction");

        Assert.IsType<FlowSplitter>(Node(world, "junction"));
    }

    [Fact]
    public void A_merger_takes_evenly_from_its_inputs()
    {
        TestWorld world = new TestWorld().Merger("merger");

        Assert.IsType<FlowMerger>(Node(world, "merger"));
    }

    [Fact]
    public void A_belt_carries_solids()
    {
        TestWorld world = new TestWorld().Belt("belt");

        Assert.Equal(Carries.Solids, Assert.IsType<FlowConduit>(Node(world, "belt")).Carries);
    }

    [Fact]
    public void A_pipe_carries_fluids()
    {
        TestWorld world = new TestWorld().Pipe("pipe");

        Assert.Equal(Carries.Fluids, Assert.IsType<FlowConduit>(Node(world, "pipe")).Carries);
    }

    [Fact]
    public void A_container_nothing_feeds_supplies_solids()
    {
        TestWorld world = new TestWorld().Container("box").Belt("out").Connect("box", "out");

        Assert.Equal(Carries.Solids, Assert.IsType<FlowUnknownSource>(Node(world, "box")).Carries);
    }

    [Fact]
    public void A_fluid_buffer_at_the_end_of_a_pipe_takes_everything()
    {
        TestWorld world = new TestWorld().Pipe("pipe").FluidBuffer("tank").Connect("pipe", "tank");

        Assert.IsType<FlowSink>(Node(world, "tank"));
    }

    [Fact]
    public void A_fluid_buffer_inside_a_line_passes_fluids_through()
    {
        TestWorld world = new TestWorld().Pipe("in").FluidBuffer("tank").Pipe("out").Connect("in", "tank", "out");

        Assert.Equal(Carries.Fluids, Assert.IsType<FlowConduit>(Node(world, "tank")).Carries);
    }

    [Fact]
    public void The_network_knows_which_items_are_fluids()
    {
        Assert.Equal([Water], WorldFlow.From(new TestWorld().Build(), [Water]).Fluids);
    }

    [Fact]
    public void A_world_from_a_node_to_a_sink_runs_its_smelter_fully()
    {
        WorldGraph world = new TestWorld()
            .Miner("miner", Items.IronOre, 60).Belt("ore", 60).Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30)
            .Belt("ingots", 60).Sink("sink")
            .Connect("miner", "ore", "smelter", "ingots", "sink")
            .Build();

        FlowResult result = SteadyState.Of(WorldFlow.From(world, fluids: []));

        Assert.Equal(1, result.Machine(BuildableId.Of("smelter")).Utilisation, 0.0001);
    }
}
