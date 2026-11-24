using SatisfactoryLedger.World.Map;
using SatisfactoryLedger.World.Snapshots;

using static SatisfactoryLedger.World.Tests.TestWorldSnapshot;

namespace SatisfactoryLedger.World.Tests;

/// <summary>What the world map draws of a snapshot: its machines, providers, belts and pipes, where they stand.</summary>
public class WorldMapTests
{
    private static readonly WorldMapResponse Map = WorldMap.For(Document);

    [Fact]
    public void Every_machine_is_on_the_map_where_it_stands()
    {
        MapMachine smelterA = Assert.Single(Map.Machines, machine => machine.Id == SmelterA);

        Assert.Equal(5, Map.Machines.Count);
        Assert.Equal(Document.Buildables.Single(buildable => buildable.Id == SmelterA).Position, smelterA.Position);
        Assert.Equal(("Build_SmelterMk1_C", "Recipe_IngotIron_C"), (smelterA.ClassName, smelterA.Recipe));
    }

    [Fact]
    public void A_generator_is_a_consumer_that_says_it_is_a_generator()
    {
        WorldMapResponse map = WorldMap.For(WithGenerators);

        Assert.True(Assert.Single(map.Consumers, consumer => consumer.Id == NuclearPlant).IsGenerator);
        Assert.DoesNotContain(map.Consumers, consumer => consumer.ClassName == "Build_ResourceSink_C" && consumer.IsGenerator);
    }

    [Fact]
    public void A_miner_shows_the_resource_and_the_purity_of_its_node()
    {
        MapProvider miner = Assert.Single(Map.Providers, provider => provider.Id == Miner);

        Assert.Equal(("Desc_OreIron_C", "BP_ResourceNode35", "normal"), (miner.Resource, miner.Node, miner.Purity));
    }

    [Fact]
    public void A_water_extractor_shows_water_and_no_purity()
    {
        // Its "node" is the water volume it stands in, which has no purity.
        MapProvider extractor = Assert.Single(Map.Providers, provider => provider.Id == WaterExtractor);

        Assert.Equal(("Desc_Water_C", (string?)null), (extractor.Resource, extractor.Purity));
    }

    [Fact]
    public void A_station_is_on_the_map_as_a_provider_without_a_resource()
    {
        MapProvider port = Assert.Single(Map.Providers, provider => provider.Id == Id("Build_DroneStation_C_2147399058"));

        Assert.Null(port.Resource);
    }

    [Fact]
    public void A_belt_is_on_the_map_with_its_curve_as_built()
    {
        MapConveyor belt = Assert.Single(Map.Conveyors, conveyor => conveyor.Id == Id("Build_ConveyorBeltMk1_C_2147303056"));

        Assert.Equal((MapConveyorKind.Belt, 8), (belt.Kind, belt.Curve.Count));
    }

    [Fact]
    public void A_pipe_is_on_the_map_as_a_pipe()
    {
        MapConveyor pipe = Assert.Single(Map.Conveyors, conveyor => conveyor.Id == Id("Build_PipelineMK2_NoIndicator_C_2147257019"));

        Assert.Equal(MapConveyorKind.Pipe, pipe.Kind);
    }

    [Fact]
    public void A_pipe_shows_the_fluid_in_it_in_cubic_metres()
    {
        MapConveyor pipe = Assert.Single(Map.Conveyors, conveyor => conveyor.Id == Id("Build_Pipeline_C_2147257932"));

        Assert.Equal(92.66m, pipe.Contents!["Desc_Water_C"], 2);
    }

    [Fact]
    public void A_pipe_the_water_extractor_feeds_has_head_lift_to_spare()
    {
        MapConveyor pipe = Assert.Single(Map.Conveyors, conveyor => conveyor.Id == Id("Build_Pipeline_C_2147257932"));

        // It climbs 4.3 m above its end at the extractor, which lifts water 10 m.
        Assert.Equal(5.7, pipe.HeadLiftToSpare!.Value, 1);
    }

    [Fact]
    public void A_pipe_of_a_network_carrying_gas_has_no_head_lift()
    {
        // As if water were a gas: gas needs no head lift (asked 2026-11-24).
        CatalogCopy waterAsGas = TestWorldSnapshot.Catalog with
        {
            Items = [.. TestWorldSnapshot.Catalog.Items.Select(item => item.Id == "Desc_Water_C" ? item with { IsGas = true } : item)],
        };

        WorldMapResponse map = WorldMap.For(Stored(waterAsGas));

        Assert.Null(Assert.Single(map.Conveyors, conveyor => conveyor.Id == Id("Build_Pipeline_C_2147257932")).HeadLiftToSpare);
    }

    [Fact]
    public void A_pipe_a_machine_feeds_has_head_lift_while_its_recipe_makes_a_fluid()
    {
        Assert.All(PipesFedBy(WithGenerators, TurbofuelPackager), pipe => Assert.NotNull(pipe.HeadLiftToSpare));
    }

    [Fact]
    public void A_machine_whose_recipe_makes_no_fluid_lifts_nothing_into_the_pipe_on_its_fluid_port()
    {
        // As an all-purpose blueprint's refinery on Pure Iron Ingot: its fluid output stays empty (asked 2026-11-24). Packaging
        // water, already in the world, makes no fluid.
        SnapshotDocument packagingWater = WithGenerators with
        {
            Buildables = [.. WithGenerators.Buildables.Select(buildable => buildable.Id == TurbofuelPackager ? buildable with { Recipe = PackagedWater.Id } : buildable)],
        };

        Assert.All(PipesFedBy(packagingWater, TurbofuelPackager), pipe => Assert.Null(pipe.HeadLiftToSpare));
    }

    private static List<MapConveyor> PipesFedBy(SnapshotDocument snapshot, string machine)
    {
        HashSet<string> fed = [.. snapshot.Connections.Where(connection => connection.From == machine).Select(connection => connection.To)];
        List<MapConveyor> pipes = [.. WorldMap.For(snapshot).Conveyors.Where(conveyor => conveyor.Kind == MapConveyorKind.Pipe && fed.Contains(conveyor.Id))];
        Assert.NotEmpty(pipes);
        return pipes;
    }

    [Fact]
    public void A_belt_has_no_head_lift()
    {
        Assert.All(Map.Conveyors.Where(conveyor => conveyor.Kind == MapConveyorKind.Belt), belt => Assert.Null(belt.HeadLiftToSpare));
    }

    [Fact]
    public void Splitters_and_mergers_are_not_drawn_as_conveyors()
    {
        // They have no curve: a splitter is a point on a belt.
        HashSet<string> withoutCurve = [.. Document.Buildables.Where(buildable => buildable.Curve is null).Select(buildable => buildable.Id)];

        Assert.DoesNotContain(Map.Conveyors, conveyor => withoutCurve.Contains(conveyor.Id));
        Assert.Equal(Document.Buildables.Count(buildable => buildable.Curve is not null), Map.Conveyors.Count);
    }

    [Fact]
    public void A_lift_is_drawn_as_a_belt_straight_up_from_where_it_stands()
    {
        MapConveyor lift = Map.Conveyors.Single(conveyor => conveyor.Id == Id("Build_ConveyorLiftMk1_C_2147312952"));

        Assert.Equal(MapConveyorKind.Belt, lift.Kind);
        Assert.Equal(400, lift.Curve[1].Location.Z - lift.Curve[0].Location.Z, 3);
    }

    [Fact]
    public void A_machine_faces_the_way_it_was_built()
    {
        Assert.Equal(90, Map.Machines.Single(machine => machine.Id == SmelterA).Yaw, 3);
    }

    [Fact]
    public void Splitters_and_mergers_are_on_the_map_where_belts_meet_them()
    {
        HashSet<string> logistics = [.. Map.Logistics.Select(buildable => buildable.Id)];

        Assert.Contains(Id("Build_ConveyorAttachmentSplitter_C_2147304077"), logistics);
        Assert.Contains(Map.Logistics, buildable => buildable.ClassName == "Build_ConveyorAttachmentMergerPriority_C");
    }

    [Fact]
    public void A_sink_is_on_the_map_as_a_consumer()
    {
        Assert.Contains(Map.Consumers, consumer => consumer.ClassName == "Build_ResourceSink_C");
    }

    [Fact]
    public void The_map_has_the_footprints_of_the_buildings_on_it()
    {
        FootprintDocument constructor = Assert.Single(Map.Footprints, footprint => footprint.Id == "Build_ConstructorMk1_C");

        Assert.Equal((-400m, 400m), (constructor.MinX, constructor.MaxX));
    }

    [Fact]
    public void The_map_has_each_footprints_volumes_to_draw_a_building_in_3D()
    {
        FootprintDocument constructor = Assert.Single(Map.Footprints, footprint => footprint.Id == "Build_ConstructorMk1_C");

        Assert.Equal(1000m, Assert.Single(constructor.Volumes!).MaxZ);
    }

    [Fact]
    public void A_belt_on_the_map_shows_what_it_carries()
    {
        MapConveyor belt = Map.Conveyors.Single(conveyor => conveyor.Id == Id("Build_ConveyorBeltMk1_C_2147303056"));

        Assert.Equal(new Dictionary<string, decimal> { ["Desc_OreIron_C"] = 7 }, belt.Contents);
    }

    [Fact]
    public void A_belt_on_the_map_says_how_many_of_its_ends_are_connected_to_nothing()
    {
        int LooseEnds(string id) => Map.Conveyors.Single(conveyor => conveyor.Id == Id(id)).LooseEnds;

        Assert.Equal((1, 0), (LooseEnds("Build_ConveyorBeltMk6_C_2147274025"), LooseEnds("Build_ConveyorBeltMk1_C_2147303056")));
    }

    [Fact]
    public void A_splitter_on_the_map_shows_what_is_in_its_buffer()
    {
        MapBuilding splitter = Map.Logistics.Single(buildable => buildable.Id == Id("Build_ConveyorAttachmentSplitter_C_2147304077"));

        Assert.Equal(new Dictionary<string, decimal> { ["Desc_OreIron_C"] = 2 }, splitter.Contents);
    }

    [Fact]
    public void A_machine_on_the_map_shows_its_ingredients_and_the_products_waiting_to_leave()
    {
        MapMachine smelter = Map.Machines.Single(machine => machine.Id == SmelterA);

        Assert.Equal(new Dictionary<string, decimal> { ["Desc_OreIron_C"] = 100 }, smelter.InputContents);
        Assert.Equal(new Dictionary<string, decimal> { ["Desc_IronIngot_C"] = 98 }, smelter.OutputContents);
    }
}
