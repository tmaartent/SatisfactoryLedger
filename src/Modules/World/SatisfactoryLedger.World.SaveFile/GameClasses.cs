using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.SaveFile;

internal enum BuildableRole
{
    /// <summary>Not a class the adapter knows. Ignored, unless it's connected to belts or pipes: then it's reported.</summary>
    Unknown,
    Machine,
    Provider,
    Consumer,
    Logistics,
    /// <summary>Known, in the item network, but not modelled yet. Reported, and its connections are left out.</summary>
    Unsupported,
}

/// <summary>Which way items or fluid move through a port.</summary>
internal enum Port
{
    In,
    Out,
    /// <summary>A pipe end: fluid can go either way, so the direction comes from the machines around it.</summary>
    Undirected,
}

/// <summary>The game knowledge of the anti-corruption layer: what a building class is, and which way its ports point.</summary>
internal static class GameClasses
{
    private static readonly HashSet<string> Machines =
    [
        "Build_SmelterMk1_C", "Build_FoundryMk1_C", "Build_ConstructorMk1_C", "Build_AssemblerMk1_C",
        "Build_ManufacturerMk1_C", "Build_OilRefinery_C", "Build_Packager_C", "Build_Blender_C",
        "Build_HadronCollider_C", "Build_Converter_C", "Build_QuantumEncoder_C",
    ];

    private static readonly HashSet<string> Providers =
    [
        "Build_MinerMk1_C", "Build_MinerMk2_C", "Build_MinerMk3_C",
        "Build_OilPump_C", "Build_WaterPump_C", "Build_FrackingExtractor_C",
    ];

    private static readonly HashSet<string> Consumers =
    [
        "Build_ResourceSink_C", "Build_CentralStorage_C",
        "Build_GeneratorCoal_C", "Build_GeneratorFuel_C", "Build_GeneratorNuclear_C",
        "Build_GeneratorBiomass_Automated_C", "Build_AlienPowerBuilding_C", "Build_Portal_C",
    ];

    /// <summary>Stations load or unload per platform (BR-W1). That isn't modelled yet.</summary>
    private static readonly HashSet<string> Unsupported =
    [
        "Build_TrainDockingStation_C", "Build_TrainDockingStationLiquid_C", "Build_TruckStation_C", "Build_DroneStation_C",
    ];

    /// <summary>The single unnamed fluid port (<c>FGPipeConnectionFactory</c>) puts fluid out on these classes; elsewhere it takes fluid in.</summary>
    private static readonly HashSet<string> FluidPortIsOutput = ["Build_Converter_C"];

    public static BuildableRole RoleOf(string className)
    {
        if (Machines.Contains(className))
        {
            return BuildableRole.Machine;
        }

        if (Providers.Contains(className))
        {
            return BuildableRole.Provider;
        }

        if (Consumers.Contains(className))
        {
            return BuildableRole.Consumer;
        }

        if (Unsupported.Contains(className))
        {
            return BuildableRole.Unsupported;
        }

        return TryLogisticsKindOf(className) is null ? BuildableRole.Unknown : BuildableRole.Logistics;
    }

    /// <summary>The item a provider extracts when its class decides it, not its node: a water extractor pumps water.</summary>
    public static string? FixedResourceOf(string className) => className == "Build_WaterPump_C" ? "Desc_Water_C" : null;

    public static LogisticsKind LogisticsKindOf(string className) =>
        TryLogisticsKindOf(className) ?? throw new ArgumentException($"{className} is not logistics", nameof(className));

    private static LogisticsKind? TryLogisticsKindOf(string className) => className switch
    {
        _ when className.StartsWith("Build_ConveyorBeltMk", StringComparison.Ordinal) => LogisticsKind.Belt,
        _ when className.StartsWith("Build_ConveyorLiftMk", StringComparison.Ordinal) => LogisticsKind.Lift,
        _ when className.StartsWith("Build_ConveyorAttachmentSplitter", StringComparison.Ordinal) => LogisticsKind.Splitter,
        _ when className.StartsWith("Build_ConveyorAttachmentMerger", StringComparison.Ordinal) => LogisticsKind.Merger,
        _ when className.StartsWith("Build_StorageContainer", StringComparison.Ordinal) => LogisticsKind.StorageContainer,
        "Build_IndustrialTank_C" or "Build_PipeStorageTank_C" => LogisticsKind.FluidBuffer,
        _ when className.StartsWith("Build_PipelineJunction", StringComparison.Ordinal) => LogisticsKind.PipeJunction,
        // Listed one by one: "Build_Pipeline…" also matches supports and flow indicators, which carry nothing.
        "Build_Pipeline_C" or "Build_Pipeline_NoIndicator_C" or "Build_PipelineMK2_C" or "Build_PipelineMK2_NoIndicator_C"
            or "Build_PipelinePump_C" or "Build_PipelinePumpMk2_C" or "Build_Valve_C" => LogisticsKind.Pipe,
        _ => null,
    };

    /// <summary>
    /// The direction of a port, from its name: <c>Input0</c>, <c>Output2</c>, a belt's <c>ConveyorAny0</c> (where items
    /// go in) and <c>ConveyorAny1</c> (where they come out), and the pipe ports of machines. Pipe ends have no direction.
    /// </summary>
    public static Port DirectionOf(string portName, string className) => portName switch
    {
        "ConveyorAny0" => Port.In,
        "ConveyorAny1" => Port.Out,
        "FGPipeConnectionFactory" => FluidPortIsOutput.Contains(className) || Providers.Contains(className) ? Port.Out : Port.In,
        _ when StartsWithAny(portName, "Input", "ConveyorInput", "PipeInput", "PipeFactoryInput") => Port.In,
        _ when StartsWithAny(portName, "Output", "PipeOutput", "PipeFactoryOutput") => Port.Out,
        _ when StartsWithAny(portName, "PipelineConnection", "Connection") => Port.Undirected,
        _ => throw new ArgumentException($"Port {portName} of {className} has no known direction", nameof(portName)),
    };

    private static bool StartsWithAny(string value, params string[] prefixes) =>
        prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal));
}
