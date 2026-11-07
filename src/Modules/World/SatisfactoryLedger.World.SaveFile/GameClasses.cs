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
    /// <summary>A train platform, drone port or truck station: a provider or a consumer, as its linked ports say.</summary>
    Station,
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

    /// <summary>Generators that burn fuel (BR-A8): the Catalog knows their burn rates. Geothermal burns nothing.</summary>
    private static readonly HashSet<string> Generators =
    [
        "Build_GeneratorCoal_C", "Build_GeneratorFuel_C", "Build_GeneratorNuclear_C", "Build_GeneratorBiomass_Automated_C",
    ];

    public static bool IsGenerator(string className) => Generators.Contains(className);

    /// <summary>A dimensional depot uploader: takes only what's left over (BR-A8).</summary>
    public static bool IsUploader(string className) => className == "Build_CentralStorage_C";

    private static readonly HashSet<string> Consumers =
    [
        "Build_ResourceSink_C", "Build_CentralStorage_C",
        "Build_GeneratorCoal_C", "Build_GeneratorFuel_C", "Build_GeneratorNuclear_C",
        "Build_GeneratorBiomass_Automated_C", "Build_AlienPowerBuilding_C", "Build_Portal_C",
    ];

    /// <summary>
    /// Stations, with the inventory that holds what they unload. Whether one unloads or loads, its ports decide
    /// (BR-W1, BR-W8): a drone port keeps what drones brought apart from what it sends and its fuel.
    /// </summary>
    private static readonly Dictionary<string, string> Stations = new(StringComparer.Ordinal)
    {
        ["Build_TrainDockingStation_C"] = "inventory",
        ["Build_TrainDockingStationLiquid_C"] = "inventory",
        ["Build_TruckStation_C"] = "inventory",
        ["Build_FluidTruckStation_C"] = "inventory",
        ["Build_DroneStation_C"] = "OutputInventory",
    };

    /// <summary>Known, in the item network, but not modelled yet. None at the moment.</summary>
    private static readonly HashSet<string> Unsupported = [];

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

        if (Stations.ContainsKey(className))
        {
            return BuildableRole.Station;
        }

        if (Unsupported.Contains(className))
        {
            return BuildableRole.Unsupported;
        }

        return TryLogisticsKindOf(className) is null ? BuildableRole.Unknown : BuildableRole.Logistics;
    }

    /// <summary>
    /// The inventory component that holds what a buildable can supply: a station's cargo, a storage container's content.
    /// Null for everything else. Fluid buffers keep their fluid elsewhere, and it isn't read yet.
    /// </summary>
    public static string? StockInventoryOf(string className) =>
        Stations.TryGetValue(className, out string? inventory) ? inventory
        : className.StartsWith("Build_StorageContainer", StringComparison.Ordinal) ? "StorageInventory"
        : null;

    /// <summary>
    /// Which output of a splitter (<c>Output1</c> is 0) or input of a merger (<c>Input1</c> is 0) a port is: a smart
    /// splitter's rules and a priority merger's priorities count them like that. Null for every other port.
    /// </summary>
    public static int? PortIndexOf(string portName, string className)
    {
        string? prefix = TryLogisticsKindOf(className) switch
        {
            LogisticsKind.Splitter => "Output",
            LogisticsKind.Merger => "Input",
            _ => null,
        };

        return prefix is not null && portName.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(portName.AsSpan(prefix.Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int number)
                ? number - 1
                : null;
    }

    /// <summary>
    /// A smart splitter's rule as the save stores it: the class of what it sends (<c>Desc_Wildcard_C</c> for any,
    /// <c>Desc_None_C</c>, <c>Desc_Overflow_C</c>, <c>Desc_AnyUndefined_C</c>, or an item) and the output.
    /// </summary>
    public static SortRule SortRuleOf(int output, string itemClass) => itemClass switch
    {
        "Desc_Wildcard_C" => new SortRule(output, SortRuleKind.Any),
        "Desc_AnyUndefined_C" => new SortRule(output, SortRuleKind.AnyUndefined),
        "Desc_Overflow_C" => new SortRule(output, SortRuleKind.Overflow),
        "Desc_None_C" => new SortRule(output, SortRuleKind.None),
        _ => new SortRule(output, SortRuleKind.Item, ItemId.Of(itemClass)),
    };

    /// <summary>The inventory a drone port or truck station keeps its fuel in. Null for buildables that burn nothing.</summary>
    public static string? FuelInventoryOf(string className) =>
        className is "Build_DroneStation_C" or "Build_TruckStation_C" or "Build_FluidTruckStation_C" ? "FuelInventory" : null;

    /// <summary>Fluid platforms and fluid truck stations count their fluid in litres; the Catalog's rates are in m³.</summary>
    public static bool StocksLitres(string className) =>
        className is "Build_TrainDockingStationLiquid_C" or "Build_FluidTruckStation_C";

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
        // FuelInput: the fuel belt of a fluid truck station.
        _ when StartsWithAny(portName, "Input", "ConveyorInput", "PipeInput", "PipeFactoryInput", "FuelInput") => Port.In,
        _ when StartsWithAny(portName, "Output", "PipeOutput", "PipeFactoryOutput") => Port.Out,
        _ when StartsWithAny(portName, "PipelineConnection", "Connection") => Port.Undirected,
        _ => throw new ArgumentException($"Port {portName} of {className} has no known direction", nameof(portName)),
    };

    private static bool StartsWithAny(string value, params string[] prefixes) =>
        prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal));
}
