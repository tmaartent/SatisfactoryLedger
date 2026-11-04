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
    [InlineData("Build_ConveyorAttachmentMerger_C_2147295890", "Build_ConveyorAttachmentMerger_C", LogisticsKind.Merger)]
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
    public void Water_flows_from_the_extractor_through_both_pipelines_into_the_packager()
    {
        Assert.Contains(new Connection(WaterExtractor, Pipeline), Save.Connections);
        Assert.Contains(new Connection(Pipeline, CleanPipeline), Save.Connections);
        Assert.Contains(new Connection(CleanPipeline, Packager), Save.Connections);
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
}
