using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain.Tracing;

namespace SatisfactoryLedger.World.Domain.Tests.Tracing;

/// <summary>
/// What the analysis found, as every finding and trace reads it (decided 2026-11-26): what logistics carries, what reaches
/// a buildable over its lines, what a machine takes and sends on.
/// </summary>
public class FlowReadingsTests
{
    private static BuildableId Id(string id) => BuildableId.Of(id);

    [Fact]
    public void A_belt_carries_what_the_analysis_found_flowing_into_it()
    {
        WorldGraph world = new TestWorld().Belt("belt").Build();
        FlowReadings readings = new TestReadings().Into("belt", Items.Coal, 45).Build(world);

        Assert.Equal(45, readings.Carries(Id("belt"), Items.Coal));
    }

    [Fact]
    public void A_merger_carries_what_the_belts_feeding_it_carry()
    {
        WorldGraph world = new TestWorld().Belt("a").Belt("b").Merger("merger").Connect("a", "merger").Connect("b", "merger").Build();
        FlowReadings readings = new TestReadings().Into("a", Items.Coal, 20).Into("b", Items.Coal, 25).Build(world);

        Assert.Equal(45, readings.Carries(Id("merger"), Items.Coal));
    }

    [Fact]
    public void A_merger_fed_by_a_merger_carries_what_both_lines_bring()
    {
        WorldGraph world = new TestWorld().Belt("a").Belt("b").Belt("c").Merger("first").Merger("second")
            .Connect("a", "first").Connect("b", "first").Connect("first", "second").Connect("c", "second").Build();
        FlowReadings readings = new TestReadings().Into("a", Items.Coal, 10).Into("b", Items.Coal, 20).Into("c", Items.Coal, 30).Build(world);

        Assert.Equal(60, readings.Carries(Id("second"), Items.Coal));
    }

    [Fact]
    public void A_pipe_network_fed_through_a_junction_carries_what_the_pipes_before_the_junction_carry()
    {
        WorldGraph world = new TestWorld().Pipe("a").Pipe("b").Junction("junction").PipeNetwork("network")
            .Connect("a", "junction").Connect("b", "junction").Connect("junction", "network").Build();
        FlowReadings readings = new TestReadings().Into("a", Items.Water, 100).Into("b", Items.Water, 50).Build(world);

        Assert.Equal(150, readings.Carries(Id("network"), Items.Water));
    }

    [Fact]
    public void A_belt_has_room_for_its_capacity_less_all_it_carries()
    {
        WorldGraph world = new TestWorld().Belt("belt", 60).Build();
        FlowReadings readings = new TestReadings().Into("belt", Items.Coal, 20).Into("belt", Items.IronOre, 15).Build(world);

        Assert.Equal(25, readings.Room(Id("belt")));
    }

    [Fact]
    public void A_splitter_has_no_limit_of_its_own()
    {
        WorldGraph world = new TestWorld().Splitter("splitter").Build();

        Assert.Equal(double.PositiveInfinity, new TestReadings().Build(world).Room(Id("splitter")));
    }

    [Fact]
    public void A_machine_takes_its_demand_at_the_rate_it_runs()
    {
        WorldGraph world = new TestWorld().Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30).Build();
        FlowReadings readings = new TestReadings().Starved("smelter", Items.IronOre, 0.5).Build(world);

        Assert.Equal(15, readings.Takes(Id("smelter"), Items.IronOre));
    }

    [Fact]
    public void A_machine_the_analysis_has_no_state_for_takes_and_sends_nothing()
    {
        WorldGraph world = new TestWorld().Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30).Build();
        FlowReadings readings = new TestReadings().Build(world);

        Assert.Equal((0.0, 0.0), (readings.Takes(Id("smelter"), Items.IronOre), readings.OutOf(Id("smelter"), Items.IronIngot)));
    }

    [Fact]
    public void A_machine_sends_on_what_it_makes_at_the_rate_it_runs()
    {
        WorldGraph world = new TestWorld().Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30).Build();
        FlowReadings readings = new TestReadings().Blocked("smelter", Items.IronIngot, 0.25).Build(world);

        Assert.Equal(7.5, readings.OutOf(Id("smelter"), Items.IronIngot));
    }

    [Fact]
    public void A_machine_would_make_more_at_full_speed_what_it_leaves_unmade()
    {
        WorldGraph world = new TestWorld().Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30).Build();
        FlowReadings readings = new TestReadings().Blocked("smelter", Items.IronIngot, 0.25).Build(world);

        Assert.Equal(22.5, readings.MakesMore(Id("smelter"), Items.IronIngot));
    }

    [Fact]
    public void What_reaches_a_drone_port_is_what_the_belts_ending_at_it_carry()
    {
        WorldGraph world = new TestWorld().Belt("a").Belt("b").LoadingStationBurning("port", Items.Battery, Rate.PerMinute(2))
            .Connect("a", "port").Connect("b", "port").Build();
        FlowReadings readings = new TestReadings().Into("a", Items.Battery, 1.5).Into("b", Items.Battery, 0.5).Build(world);

        Assert.Equal(2, readings.Reaching(Id("port"), Items.Battery));
    }

    [Fact]
    public void What_reaches_a_machine_over_its_belts_leaves_out_what_it_takes_from_its_stock()
    {
        WorldGraph world = new TestWorld().Belt("ore").Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30).Connect("ore", "smelter").Build();
        FlowReadings readings = new TestReadings().Running("smelter").Into("ore", Items.IronOre, 10).Build(world);

        Assert.Equal((10.0, 30.0), (readings.Reaching(Id("smelter"), Items.IronOre), readings.Takes(Id("smelter"), Items.IronOre)));
    }

    [Fact]
    public void What_reaches_a_machine_from_a_pipe_network_others_share_is_what_it_takes()
    {
        WorldGraph world = new TestWorld().Pipe("in").PipeNetwork("network")
            .Machine("refinery", Items.Water, 60, Items.Coal, 30).Machine("other", Items.Water, 60, Items.Coal, 30)
            .Connect("in", "network").Connect("network", "refinery").Connect("network", "other").Build();
        FlowReadings readings = new TestReadings().Starved("refinery", Items.Water, 0.5).Into("in", Items.Water, 90).Build(world);

        Assert.Equal(30, readings.Reaching(Id("refinery"), Items.Water));
    }

    [Fact]
    public void What_flows_into_a_machine_is_what_it_takes_and_into_a_belt_what_it_carries()
    {
        WorldGraph world = new TestWorld().Belt("ore").Machine("smelter", Items.IronOre, 30, Items.IronIngot, 30).Connect("ore", "smelter").Build();
        FlowReadings readings = new TestReadings().Running("smelter").Into("ore", Items.IronOre, 10).Build(world);

        Assert.Equal((30.0, 10.0), (readings.Into(Id("smelter"), Items.IronOre), readings.Into(Id("ore"), Items.IronOre)));
    }

    [Fact]
    public void What_flows_into_a_drone_port_is_what_reaches_it()
    {
        WorldGraph world = new TestWorld().Belt("a").LoadingStationBurning("port", Items.Battery, Rate.PerMinute(2)).Connect("a", "port").Build();
        FlowReadings readings = new TestReadings().Into("a", Items.Battery, 1.5).Build(world);

        Assert.Equal(1.5, readings.Into(Id("port"), Items.Battery));
    }
}
