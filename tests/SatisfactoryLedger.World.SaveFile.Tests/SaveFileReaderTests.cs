using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain;

using static SatisfactoryLedger.World.SaveFile.Tests.TestWorldSave;

namespace SatisfactoryLedger.World.SaveFile.Tests;

/// <summary>Reading the test world (ADR-0013): what the anti-corruption layer makes of a real 1.2 save.</summary>
public class SaveFileReaderTests
{
    [Fact]
    public void A_save_tells_its_session_name_and_game_build()
    {
        Assert.Equal("Development", Save.Info.SessionName);
        Assert.Equal(517554, Save.Info.GameBuild);
    }

    [Fact]
    public void The_smelters_constructors_and_packager_are_machines_with_their_recipes()
    {
        Dictionary<BuildableId, string?> recipes = Save.Buildables.OfType<SavedMachine>()
            .ToDictionary(machine => machine.Id, machine => machine.Recipe);

        Assert.Equal(
            new Dictionary<BuildableId, string?>
            {
                [SmelterA] = "Recipe_IngotIron_C",
                [SmelterB] = "Recipe_IngotIron_C",
                [ConstructorC] = "Recipe_IronPlate_C",
                [ConstructorD] = "Recipe_IronRod_C",
                [Packager] = "Recipe_PackagedWater_C",
            },
            recipes);
    }

    [Fact]
    public void A_machine_without_a_saved_clock_speed_runs_at_100_percent()
    {
        Assert.Equal(ClockSpeed.Percent(100), Get<SavedMachine>(SmelterA).ClockSpeed);
    }

    [Fact]
    public void A_saved_clock_speed_is_read_to_the_game_precision_of_four_decimals()
    {
        // The save stores the float 0.333333; the game shows 33.3333%.
        Assert.Equal(ClockSpeed.Percent(33.3333m), Get<SavedMachine>(SmelterB).ClockSpeed);
    }

    [Fact]
    public void An_underclocked_constructor_keeps_its_clock_speed()
    {
        Assert.Equal(ClockSpeed.Percent(75), Get<SavedMachine>(ConstructorC).ClockSpeed);
    }

    [Fact]
    public void A_machine_without_somersloops_has_no_production_boost()
    {
        Assert.Equal(1m, Get<SavedMachine>(ConstructorC).ProductionBoost);
    }

    [Fact]
    public void The_miner_is_a_provider_on_its_resource_node()
    {
        SavedProvider miner = Get<SavedProvider>(Miner);

        Assert.Equal("Build_MinerMk1_C", miner.ClassName);
        Assert.Equal("BP_ResourceNode35", miner.Node);
        Assert.Equal(ClockSpeed.Percent(100), miner.ClockSpeed);
    }

    [Fact]
    public void A_buildable_stands_where_the_save_puts_it_in_game_units()
    {
        SavedPosition position = Get<SavedProvider>(Miner).Position!;

        Assert.Equal(256611.66, position.X, tolerance: 0.01);
        Assert.Equal(-197978.56, position.Y, tolerance: 0.01);
        Assert.Equal(3700.01, position.Z, tolerance: 0.01);
    }

    [Fact]
    public void The_water_extractor_is_a_provider()
    {
        Assert.Equal("Build_WaterPump_C", Get<SavedProvider>(WaterExtractor).ClassName);
    }

    [Fact]
    public void A_water_extractor_pumps_water_whatever_it_stands_in()
    {
        Assert.Equal("Desc_Water_C", Get<SavedProvider>(WaterExtractor).Resource);
    }

    [Fact]
    public void What_a_miner_extracts_depends_on_its_node_so_the_adapter_leaves_it_open()
    {
        Assert.Null(Get<SavedProvider>(Miner).Resource);
    }

    [Fact]
    public void A_freight_platform_with_a_belt_on_its_output_unloads_so_it_is_a_provider()
    {
        Assert.Equal("Build_TrainDockingStation_C", Get<SavedProvider>(FreightPlatformUnloading).ClassName);
    }

    [Fact]
    public void A_freight_platform_with_a_belt_on_its_input_loads_so_it_is_a_consumer()
    {
        Assert.Equal("Build_TrainDockingStation_C", Get<SavedConsumer>(FreightPlatformLoading).ClassName);
    }

    [Fact]
    public void An_unloading_platform_holds_what_is_on_it()
    {
        Assert.Equal(new Dictionary<string, decimal> { ["Desc_IronScrew_C"] = 7118 }, Get<SavedProvider>(FreightPlatformUnloading).Stock);
    }

    [Fact]
    public void A_drone_port_with_belts_on_both_sides_is_a_provider_holding_what_drones_brought()
    {
        // What it sends (reinforced plates) and its fuel are not what it supplies.
        Assert.Equal(new Dictionary<string, decimal> { ["Desc_ModularFrame_C"] = 121 }, Get<SavedProvider>(DronePort).Stock);
    }

    [Fact]
    public void A_truck_station_that_unloads_is_a_provider_holding_its_cargo_but_not_its_fuel()
    {
        Assert.Equal(new Dictionary<string, decimal> { ["Desc_IronPlate_C"] = 140 }, Get<SavedProvider>(TruckStationUnloading).Stock);
    }

    [Fact]
    public void A_truck_station_that_only_loads_is_a_consumer()
    {
        Assert.Equal("Build_TruckStation_C", Get<SavedConsumer>(TruckStationLoading).ClassName);
    }

    [Fact]
    public void A_drone_port_burns_the_fuel_in_its_fuel_slot()
    {
        Assert.Equal(["Desc_PackagedRocketFuel_C"], Get<SavedProvider>(DronePort).Burns!);
    }

    [Fact]
    public void A_truck_station_that_only_loads_burns_the_fuel_in_its_fuel_slot()
    {
        Assert.Equal(["Desc_Fuel_C"], Get<SavedConsumer>(TruckStationLoading).Burns!);
    }

    [Fact]
    public void A_truck_station_that_unloads_burns_the_fuel_in_its_fuel_slot()
    {
        Assert.Equal(["Desc_PackagedRocketFuel_C"], Get<SavedProvider>(TruckStationUnloading).Burns!);
    }

    [Fact]
    public void A_freight_platform_burns_nothing()
    {
        Assert.Null(Get<SavedProvider>(FreightPlatformUnloading).Burns);
    }

    [Fact]
    public void A_storage_container_holds_its_stock()
    {
        Assert.Equal(new Dictionary<string, decimal> { ["Desc_IronScrew_C"] = 1013 }, Get<SavedLogistics>(ScrewContainer).Stock);
    }

    [Fact]
    public void An_empty_storage_container_holds_nothing()
    {
        Assert.Empty(Get<SavedLogistics>(Id("Build_StorageContainerMk2_C_2147239114")).Stock!);
    }

    [Fact]
    public void A_station_is_connected_to_its_belts()
    {
        Assert.Contains(new Connection(FreightPlatformUnloading, Id("Build_ConveyorBeltMk1_C_2147417128")), Save.Connections);
        Assert.Contains(new Connection(Id("Build_ConveyorBeltMk1_C_2147401246"), FreightPlatformLoading), Save.Connections);
    }

    [Fact]
    public void The_smart_splitter_keeps_its_rules_per_output()
    {
        Assert.Equal(
            [new SortRule(0, SortRuleKind.Any), new SortRule(2, SortRuleKind.None), new SortRule(1, SortRuleKind.Overflow)],
            Get<SavedLogistics>(SmartSplitter).SortRules!);
    }

    [Fact]
    public void A_plain_splitter_has_no_rules()
    {
        Assert.Null(Get<SavedLogistics>(Splitter).SortRules);
    }

    [Fact]
    public void A_belt_leaving_a_smart_splitter_knows_which_output_it_leaves_from()
    {
        // Output 0 (any) goes to the container, output 1 (overflow) to the sink, as the test world was built.
        Assert.Contains(new Connection(SmartSplitter, BeltIntoContainer, FromPort: 0), Save.Connections);
        Assert.Contains(new Connection(SmartSplitter, Id("Build_ConveyorBeltMk1_C_2147281116"), FromPort: 1), Save.Connections);
    }

    [Fact]
    public void A_belt_entering_a_merger_knows_which_input_it_enters_by()
    {
        Assert.All(Save.Connections.Where(connection => connection.To == PriorityMerger),
            connection => Assert.NotNull(connection.ToPort));
    }

    [Fact]
    public void A_priority_merger_keeps_its_priority_per_input()
    {
        // Smelter B's belt is on the center input, Input1, which has the highest priority in the game.
        Assert.Equal([2, 0, 0], Get<SavedLogistics>(PriorityMerger).InputPriorities!);
        Assert.Contains(new Connection(Id("Build_ConveyorBeltMk1_C_2147295053"), PriorityMerger, ToPort: 0), Save.Connections);
    }

    [Fact]
    public void A_plain_merger_has_no_priorities()
    {
        Assert.All(Save.Buildables.OfType<SavedLogistics>().Where(logistics => logistics.ClassName == "Build_ConveyorAttachmentMerger_C"),
            merger => Assert.Null(merger.InputPriorities));
    }

    [Fact]
    public void A_fluid_truck_station_that_unloads_is_a_provider_holding_its_fluid_in_cubic_metres()
    {
        SavedProvider station = Get<SavedProvider>(FluidTruckStationUnloading);

        Assert.Equal(new Dictionary<string, decimal> { ["Desc_Water_C"] = 680.37m }, station.Stock);
        Assert.Equal(["Desc_PackagedRocketFuel_C"], station.Burns!);
    }

    [Fact]
    public void A_fluid_truck_station_that_loads_is_a_consumer_that_burns_its_fuel()
    {
        Assert.Equal(["Desc_PackagedRocketFuel_C"], Get<SavedConsumer>(FluidTruckStationLoading).Burns!);
    }

    [Fact]
    public void A_fluid_truck_station_is_fed_fuel_over_its_fuel_belt()
    {
        Assert.Contains(new Connection(Id("Build_ConveyorBeltMk1_C_2147393153"), FluidTruckStationUnloading), Save.Connections);
    }

    [Fact]
    public void A_fluid_buffer_holds_the_fluid_of_its_pipe_network_in_cubic_metres()
    {
        Dictionary<string, decimal> stock = new(Get<SavedLogistics>(FluidBuffer).Stock!);

        Assert.Equal(["Desc_Water_C"], stock.Keys);
        Assert.Equal(404, stock["Desc_Water_C"], 0);
    }

    [Fact]
    public void The_sink_is_a_consumer()
    {
        Assert.Equal("Build_ResourceSink_C", Get<SavedConsumer>(Sink).ClassName);
    }

    [Theory]
    [InlineData("Build_ConveyorBeltMk1_C_2147317296", "Build_ConveyorBeltMk1_C", LogisticsKind.Belt)]
    [InlineData("Build_ConveyorLiftMk1_C_2147312952", "Build_ConveyorLiftMk1_C", LogisticsKind.Lift)]
    [InlineData("Build_ConveyorBeltMk6_C_2147290960", "Build_ConveyorBeltMk6_C", LogisticsKind.Belt)]
    [InlineData("Build_ConveyorAttachmentSplitter_C_2147304077", "Build_ConveyorAttachmentSplitter_C", LogisticsKind.Splitter)]
    [InlineData("Build_ConveyorAttachmentSplitterSmart_C_2147286684", "Build_ConveyorAttachmentSplitterSmart_C", LogisticsKind.Splitter)]
    [InlineData("Build_ConveyorAttachmentMergerPriority_C_2147471421", "Build_ConveyorAttachmentMergerPriority_C", LogisticsKind.Merger)]
    [InlineData("Build_StorageContainerMk2_C_2147277594", "Build_StorageContainerMk2_C", LogisticsKind.StorageContainer)]
    [InlineData("Build_Pipeline_C_2147257932", "Build_Pipeline_C", LogisticsKind.Pipe)]
    [InlineData("Build_PipelineMK2_NoIndicator_C_2147257019", "Build_PipelineMK2_NoIndicator_C", LogisticsKind.Pipe)]
    public void Belts_lifts_splitters_mergers_containers_and_pipes_are_logistics_with_their_class(
        string name, string className, LogisticsKind kind)
    {
        SavedLogistics logistics = Get<SavedLogistics>(Id(name));

        Assert.Equal(className, logistics.ClassName);
        Assert.Equal(kind, logistics.Kind);
    }

    [Fact]
    public void Poles_supports_flow_indicators_and_the_coupon_shop_are_not_part_of_the_world()
    {
        Assert.DoesNotContain(Save.Buildables, buildable =>
            buildable.ClassName is "Build_ConveyorPole_C" or "Build_PipelineSupport_C"
                or "Build_PipelineFlowIndicator_C" or "Build_ResourceSinkShop_C");
    }

    [Fact]
    public void The_test_world_has_nothing_the_adapter_does_not_support_yet()
    {
        Assert.Empty(Save.Unsupported);
    }

    [Fact]
    public void Items_flow_from_the_miner_over_the_lift_to_the_splitter()
    {
        BuildableId beltAfterLift = Id("Build_ConveyorBeltMk1_C_2147303056");

        Assert.Contains(new Connection(Miner, FirstBelt), Save.Connections);
        Assert.Contains(new Connection(FirstBelt, Lift), Save.Connections);
        Assert.Contains(new Connection(Lift, beltAfterLift), Save.Connections);
        Assert.Contains(new Connection(beltAfterLift, Splitter), Save.Connections);
    }

    [Fact]
    public void Water_flows_from_the_extractor_through_the_pipe_network_into_the_packager()
    {
        // BR-A6: the two pipelines are one network; its hub is named after the pipe that sorts first.
        BuildableId hub = PipeNetwork.HubOf(new[] { Pipeline, CleanPipeline }.MinBy(id => id.Id, StringComparer.Ordinal)!);

        Assert.Contains(new Connection(WaterExtractor, Pipeline), Save.Connections);
        Assert.Contains(new Connection(Pipeline, hub), Save.Connections);
        Assert.Contains(new Connection(hub, CleanPipeline), Save.Connections);
        Assert.Contains(new Connection(CleanPipeline, Packager), Save.Connections);
    }

    [Fact]
    public void A_pipe_network_is_a_logistics_hub_that_stands_nowhere()
    {
        List<SavedLogistics> hubs = [.. Save.Buildables.OfType<SavedLogistics>().Where(logistics => logistics.Kind == LogisticsKind.PipeNetwork)];

        Assert.NotEmpty(hubs);
        Assert.All(hubs, hub => Assert.Equal(("PipeNetwork", (SavedPosition?)null), (hub.ClassName, hub.Position)));
    }

    [Fact]
    public void An_unconnected_input_or_belt_end_gives_no_connection()
    {
        // Constructor D: nothing feeds it, and its output belt leads nowhere.
        Assert.DoesNotContain(Save.Connections, connection => connection.To == ConstructorD);
        Assert.Single(Save.Connections, connection => connection.From == ConstructorD);
    }

    [Fact]
    public void Every_connection_is_between_buildables_of_the_world()
    {
        HashSet<BuildableId> ids = [.. Save.Buildables.Select(buildable => buildable.Id)];

        Assert.All(Save.Connections, connection =>
        {
            Assert.Contains(connection.From, ids);
            Assert.Contains(connection.To, ids);
        });
    }

    [Fact]
    public void A_file_that_is_not_a_save_is_rejected_with_a_clear_error()
    {
        using var notASave = new MemoryStream("this is not a save file"u8.ToArray());

        Assert.Throws<SaveFileException>(() => SaveFileReader.Read(notASave));
    }

    // ---- Curves: how a belt or pipe runs, as built -------------------------------------------------------------

    private static SavedLogistics Logistics(string name) =>
        Assert.IsType<SavedLogistics>(Save.Buildables.Single(buildable => buildable.Id == Id(name)));

    [Fact]
    public void A_straight_belt_runs_from_where_it_stands_to_its_end_point()
    {
        // A 9 m belt, westwards: two points, in world coordinates.
        IReadOnlyList<SavedCurvePoint> curve = Logistics("Build_ConveyorBeltMk1_C_2147299536").Curve!;

        Assert.Equal(2, curve.Count);
        Assert.Equal((254400, -198000, 4199.99), (curve[0].Location.X, curve[0].Location.Y, Math.Round(curve[0].Location.Z, 2)));
        Assert.Equal((253500, -198000), (Math.Round(curve[1].Location.X), Math.Round(curve[1].Location.Y)));
    }

    [Fact]
    public void A_belt_keeps_every_point_of_its_curve_with_its_tangents()
    {
        IReadOnlyList<SavedCurvePoint> curve = Logistics("Build_ConveyorBeltMk1_C_2147303056").Curve!;

        Assert.Equal(8, curve.Count);
        // The tangent the curve leaves its first point with: about a metre westwards, a little south.
        Assert.Equal((-99.76, -6.86), (Math.Round(curve[0].LeaveTangent.X, 2), Math.Round(curve[0].LeaveTangent.Y, 2)));
    }

    [Fact]
    public void A_pipe_keeps_its_curve_too()
    {
        IReadOnlyList<SavedCurvePoint> curve = Logistics("Build_PipelineMK2_NoIndicator_C_2147257019").Curve!;

        Assert.Equal(6, curve.Count);
        Assert.Equal(1717.48 + 198.12, Math.Round(curve[^1].Location.Z, 2), 2);
    }

    [Fact]
    public void A_splitter_has_no_curve()
    {
        Assert.All(
            Save.Buildables.OfType<SavedLogistics>().Where(logistics => logistics.Kind is LogisticsKind.Splitter or LogisticsKind.Merger or LogisticsKind.Lift),
            logistics => Assert.Null(logistics.Curve));
    }

    [Fact]
    public void A_turned_belt_turns_its_curve_with_it()
    {
        // A quarter turn about the vertical axis: what pointed along x now points along y.
        var quarterTurn = new SatisfactorySaveNet.Abstracts.Maths.Vector.Vector4(0, 0, MathF.Sqrt(0.5f), MathF.Sqrt(0.5f));

        SavedPosition turned = SaveFileReader.Rotate(quarterTurn, new SavedPosition(100, 0, 0));

        Assert.Equal((0, 100, 0), (Math.Round(turned.X, 3), Math.Round(turned.Y, 3), Math.Round(turned.Z, 3)));
    }

    // ---- Which way a buildable faces, seen from above: so the map can turn its footprint ----------------------

    [Fact]
    public void A_buildable_keeps_which_way_it_faces()
    {
        // The smelters and constructors of the main line face along y (a quarter turn); the packager is turned −100°.
        Assert.Equal(90, Math.Round(Save.Buildables.Single(buildable => buildable.Id == SmelterA).Yaw), 0);
        Assert.Equal(-100, Math.Round(Save.Buildables.Single(buildable => buildable.Id == Id("Build_Packager_C_2147260704")).Yaw), 0);
    }

    [Fact]
    public void A_quarter_turn_about_the_vertical_axis_is_a_yaw_of_90_degrees()
    {
        var quarterTurn = new SatisfactorySaveNet.Abstracts.Maths.Vector.Vector4(0, 0, MathF.Sqrt(0.5f), MathF.Sqrt(0.5f));

        Assert.Equal(90, SaveFileReader.YawOf(quarterTurn), 3);
    }

    // ---- Contents: what's on a belt, in a splitter, in a machine at save time (not stock: the analysis ignores it) -

    [Fact]
    public void A_belt_carries_the_items_its_conveyor_chain_has_on_it()
    {
        // The miner's chain: belt, lift, belt, with 12 iron ore; 7 of them on this belt.
        Assert.Equal(new Dictionary<string, decimal> { ["Desc_OreIron_C"] = 7 }, Logistics("Build_ConveyorBeltMk1_C_2147303056").Contents);
    }

    [Fact]
    public void A_lift_carries_its_part_of_the_chain()
    {
        Assert.Equal(new Dictionary<string, decimal> { ["Desc_OreIron_C"] = 3 }, Logistics("Build_ConveyorLiftMk1_C_2147312952").Contents);
    }

    [Fact]
    public void Items_are_counted_on_a_belt_whose_stretch_of_the_chain_wraps_around()
    {
        // Its items run from index 5 of the chain's ring of 10 round to index 2: 8 items.
        Assert.Equal(new Dictionary<string, decimal> { ["Desc_OreIron_C"] = 8 }, Logistics("Build_ConveyorBeltMk1_C_2147299536").Contents);
    }

    [Fact]
    public void An_empty_belt_carries_nothing()
    {
        Assert.Empty(Logistics("Build_ConveyorBeltMk1_C_2147281116").Contents!);
    }

    [Fact]
    public void A_splitter_holds_what_is_in_its_buffer()
    {
        Assert.Equal(new Dictionary<string, decimal> { ["Desc_OreIron_C"] = 2 }, Logistics("Build_ConveyorAttachmentSplitter_C_2147304077").Contents);
    }

    [Fact]
    public void A_machine_holds_its_ingredients_and_the_products_waiting_to_leave()
    {
        // Smelter A is output blocked: its ingots pile up.
        var smelter = Assert.IsType<SavedMachine>(Save.Buildables.Single(buildable => buildable.Id == SmelterA));

        Assert.Equal(new Dictionary<string, decimal> { ["Desc_OreIron_C"] = 100 }, smelter.InputContents);
        Assert.Equal(new Dictionary<string, decimal> { ["Desc_IronIngot_C"] = 98 }, smelter.OutputContents);
    }

    [Fact]
    public void Contents_are_not_stock()
    {
        // Stock is what the analysis may count as supply (BR-A9); what's on a belt never is.
        Assert.Null(Logistics("Build_ConveyorBeltMk1_C_2147303056").Stock);
    }

    // ---- Generators (BR-A8): the fuel in them at save time, and their clock speed ----------------------------

    private static SavedConsumer GeneratorIn(BuildableId id) =>
        Assert.IsType<SavedConsumer>(GeneratorsSave.Buildables.Single(buildable => buildable.Id == id));

    [Theory]
    [InlineData("Build_GeneratorNuclear_C_2147427188", "Desc_NuclearFuelRod_C")]
    [InlineData("Build_GeneratorCoal_C_2147408929", "Desc_CompactedCoal_C")]
    [InlineData("Build_GeneratorFuel_C_2147379250", "Desc_LiquidTurboFuel_C")]
    public void A_generator_burns_the_fuel_it_has_in_it(string name, string fuel)
    {
        Assert.Equal(fuel, GeneratorIn(Id(name)).Fuel);
    }

    [Fact]
    public void A_generator_without_a_saved_clock_speed_runs_at_100_percent()
    {
        Assert.Equal(ClockSpeed.Percent(100), GeneratorIn(NuclearPlant).ClockSpeed);
    }

    [Fact]
    public void A_sink_burns_nothing()
    {
        Assert.All(
            Save.Buildables.OfType<SavedConsumer>().Where(consumer => consumer.ClassName == "Build_ResourceSink_C"),
            sink => Assert.Null(sink.Fuel));
    }

    [Fact]
    public void A_generator_holds_the_fuel_in_it_and_its_water_in_litres()
    {
        var plant = Assert.IsType<SavedConsumer>(GeneratorsSave.Buildables.Single(buildable => buildable.Id == NuclearPlant));

        Assert.Equal(new Dictionary<string, decimal> { ["Desc_NuclearFuelRod_C"] = 50, ["Desc_Water_C"] = 50_000 }, plant.FuelContents);
    }

    [Fact]
    public void A_nuclear_plants_waste_leaves_on_the_belt_on_its_output()
    {
        Assert.Contains(GeneratorsSave.Connections, connection => connection.From == NuclearPlant && connection.To == Id("Build_ConveyorBeltMk6_C_2147424725"));
    }

    [Fact]
    public void A_generator_is_fed_water_by_a_pipe()
    {
        Assert.Contains(GeneratorsSave.Connections, connection => connection.From == Id("Build_PipelineMK2_C_2147412050") && connection.To == NuclearPlant);
    }
}
