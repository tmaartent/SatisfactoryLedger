using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.SaveFile.Tests;

/// <summary>The game knowledge in the anti-corruption layer: what a class is, and which way a port points.</summary>
public class GameClassesTests
{
    [Theory]
    [InlineData("Build_SmelterMk1_C", "Machine")]
    [InlineData("Build_QuantumEncoder_C", "Machine")]
    [InlineData("Build_MinerMk3_C", "Provider")]
    [InlineData("Build_OilPump_C", "Provider")]
    [InlineData("Build_FrackingExtractor_C", "Provider")]
    [InlineData("Build_ResourceSink_C", "Consumer")]
    [InlineData("Build_CentralStorage_C", "Consumer")]
    [InlineData("Build_GeneratorFuel_C", "Consumer")]
    [InlineData("Build_ConveyorBeltMk6_C", "Logistics")]
    [InlineData("Build_PipelineJunction_Cross_C", "Logistics")]
    [InlineData("Build_IndustrialTank_C", "Logistics")]
    public void Each_kind_of_building_has_its_role(string className, string role)
    {
        Assert.Equal(Enum.Parse<BuildableRole>(role), GameClasses.RoleOf(className));
    }

    // Stations load or unload per platform (BR-W1). Until that's modelled, they're reported, not guessed.
    [Theory]
    [InlineData("Build_TrainDockingStation_C")]
    [InlineData("Build_TrainDockingStationLiquid_C")]
    [InlineData("Build_TruckStation_C")]
    [InlineData("Build_DroneStation_C")]
    public void Stations_are_not_supported_yet(string className)
    {
        Assert.Equal(BuildableRole.Unsupported, GameClasses.RoleOf(className));
    }

    [Fact]
    public void A_class_the_adapter_does_not_know_has_no_role()
    {
        Assert.Equal(BuildableRole.Unknown, GameClasses.RoleOf("Build_SomethingNew_C"));
    }

    [Theory]
    [InlineData("Build_ConveyorLiftMk6_C", LogisticsKind.Lift)]
    [InlineData("Build_ConveyorAttachmentMergerPriority_C", LogisticsKind.Merger)]
    [InlineData("Build_PipelinePumpMk2_C", LogisticsKind.Pipe)]
    [InlineData("Build_Valve_C", LogisticsKind.Pipe)]
    [InlineData("Build_PipelineJunction_T_C", LogisticsKind.PipeJunction)]
    [InlineData("Build_PipeStorageTank_C", LogisticsKind.StorageContainer)]
    public void Logistics_have_their_kind(string className, LogisticsKind kind)
    {
        Assert.Equal(kind, GameClasses.LogisticsKindOf(className));
    }

    [Theory]
    [InlineData("Input0", "Build_SmelterMk1_C", "In")]
    [InlineData("Output2", "Build_SmelterMk1_C", "Out")]
    [InlineData("ConveyorAny0", "Build_ConveyorBeltMk1_C", "In")]
    [InlineData("ConveyorAny1", "Build_ConveyorBeltMk1_C", "Out")]
    [InlineData("PipeInputFactory", "Build_Packager_C", "In")]
    [InlineData("PipeOutputFactory", "Build_OilRefinery_C", "Out")]
    [InlineData("PipeInput0", "Build_QuantumEncoder_C", "In")]
    [InlineData("PipelineConnection0", "Build_Pipeline_C", "Undirected")]
    [InlineData("Connection2", "Build_PipelineJunction_Cross_C", "Undirected")]
    [InlineData("ConnectionAny1", "Build_IndustrialTank_C", "Undirected")]
    public void A_port_points_the_way_its_name_says(string portName, string className, string expected)
    {
        Assert.Equal(Enum.Parse<Port>(expected), GameClasses.DirectionOf(portName, className));
    }

    // The unnamed fluid port: an extractor and the Converter put fluid out; generators and the Particle Accelerator take it in.
    [Theory]
    [InlineData("Build_WaterPump_C", "Out")]
    [InlineData("Build_Converter_C", "Out")]
    [InlineData("Build_GeneratorFuel_C", "In")]
    [InlineData("Build_HadronCollider_C", "In")]
    public void The_unnamed_fluid_port_points_the_way_the_building_works(string className, string expected)
    {
        Assert.Equal(Enum.Parse<Port>(expected), GameClasses.DirectionOf("FGPipeConnectionFactory", className));
    }
}
