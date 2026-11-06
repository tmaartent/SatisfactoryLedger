using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;

using static SatisfactoryLedger.World.Tests.TestWorldSnapshot;

namespace SatisfactoryLedger.World.Tests;

/// <summary>
/// End to end: the test world (Fixtures/Development.sav), stored as a snapshot with the Catalog's rates, through the
/// flow analysis (BR-A1 to BR-A13).
/// </summary>
public class TestWorldFlowTests
{
    private const double Precision = 0.0001;

    private static readonly FlowResult Result = SteadyState.Of(Document.ToFlowNetwork());

    private static MachineState Machine(string id) => Result.Machine(BuildableId.Of(id));

    [Fact]
    public void The_test_world_settles()
    {
        Assert.True(Result.Settled);
    }

    [Fact]
    public void Constructor_C_is_underclocked_so_it_takes_less_than_both_smelters_make_and_runs_fully()
    {
        Assert.Equal(1, Machine(ConstructorC).Utilisation, Precision);
    }

    [Fact]
    public void Smelter_B_runs_fully_because_the_merger_takes_evenly_and_it_offers_less_than_its_share()
    {
        Assert.Equal(1, Machine(SmelterB).Utilisation, Precision);
    }

    [Fact]
    public void Smelter_A_gets_the_rest_of_what_constructor_C_takes_and_is_output_blocked()
    {
        // Constructor C takes 22.5 ingots a minute; smelter B makes 10 of them, so smelter A makes 12.5 of its 30.
        MachineState smelterA = Machine(SmelterA);

        Assert.Equal(12.5 / 30, smelterA.Utilisation, Precision);
        Assert.Equal(MachineLimit.OutputBlocked, smelterA.Limit);
    }

    [Fact]
    public void The_miner_delivers_only_what_the_smelters_make_room_for()
    {
        UnusedSupply miner = Assert.Single(Result.UnusedSupply, supply => supply.Provider == BuildableId.Of(Miner));

        Assert.Equal(60, miner.Offered, Precision);
        Assert.Equal(22.5, miner.Delivered, Precision);
    }

    [Fact]
    public void The_packager_gets_its_water_from_the_extractor_and_its_canisters_from_the_container()
    {
        UnusedSupply water = Assert.Single(Result.UnusedSupply, supply => supply.Provider == BuildableId.Of(WaterExtractor));
        UnknownSupply canisters = Assert.Single(Result.UnknownSupply);

        Assert.Equal(60, water.Delivered, Precision);
        Assert.Equal([Item("Desc_FluidCanister_C")], canisters.Delivered.Keys);
        Assert.Equal(1, Machine(Packager).Utilisation, Precision);
    }

    private static FlowNode Node(string name)
    {
        FlowNetwork network = Document.ToFlowNetwork();
        return network.Nodes[network.IndexOf[BuildableId.Of(Id(name))]];
    }

    [Fact]
    public void An_unloading_freight_platform_is_a_source_with_the_stock_on_it()
    {
        var platform = Assert.IsType<FlowUnknownSource>(Node("Build_TrainDockingStation_C_2147427754"));

        Assert.Equal(7666, platform.Stock[Item("Desc_IronScrew_C")]);
    }

    [Fact]
    public void The_drone_port_burns_rocket_fuel()
    {
        var port = Assert.IsType<FlowUnknownSource>(Node("Build_DroneStation_C_2147399058"));

        Assert.Equal([Item("Desc_PackagedRocketFuel_C")], port.Burns!);
    }

    [Fact]
    public void A_loading_freight_platform_takes_everything_like_a_consumer()
    {
        Assert.IsType<FlowSink>(Node("Build_TrainDockingStation_C_2147409101"));
    }
}
