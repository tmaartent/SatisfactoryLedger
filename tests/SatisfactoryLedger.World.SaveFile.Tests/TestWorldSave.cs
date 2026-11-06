using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.SaveFile.Tests;

/// <summary>
/// The test world (Fixtures/Development.sav), read once. What's in it:
/// <code>
/// Miner Mk.1 → Belt Mk.1 → Lift Mk.1 → Belt Mk.1 → Splitter
///   → Smelter A (100%) and Smelter B (33.3333%) → Merger → Belt Mk.6 → Constructor C (Iron Plate, 75%)
///   → Smart Splitter: overflow → AWESOME Sink, rest → belt → Storage Container
/// Constructor D (Iron Rod): input unconnected, output belt ends in the air
/// Water Extractor → Pipeline Mk.1 → Clean Pipeline Mk.2 → Packager (Packaged Water),
///   with empty canisters from a storage container and its output to another one
/// Stations, each with belts to or from containers and sinks:
///   Freight platform A unloads screws; freight platform B loads rods
///   Drone port: loads reinforced plates and rocket fuel, unloads modular frames
///   Truck station A: fed fuel, unloads iron plates; truck station B: loads screws, fed fuel
/// </code>
/// </summary>
internal static class TestWorldSave
{
    private static readonly Lazy<SavedWorld> Saved = new(() =>
    {
        using FileStream file = File.OpenRead("Fixtures/Development.sav");
        return SaveFileReader.Read(file);
    });

    public static SavedWorld Save => Saved.Value;

    /// <summary>A buildable of the test world by the last part of its path, e.g. "Build_MinerMk1_C_2147339296".</summary>
    public static BuildableId Id(string name) => BuildableId.Of($"Persistent_Level:PersistentLevel.{name}");

    public static readonly BuildableId Miner = Id("Build_MinerMk1_C_2147339296");
    public static readonly BuildableId SmelterA = Id("Build_SmelterMk1_C_2147301226");
    public static readonly BuildableId SmelterB = Id("Build_SmelterMk1_C_2147301500");
    public static readonly BuildableId ConstructorC = Id("Build_ConstructorMk1_C_2147291547");
    public static readonly BuildableId ConstructorD = Id("Build_ConstructorMk1_C_2147275293");
    public static readonly BuildableId Packager = Id("Build_Packager_C_2147260704");
    public static readonly BuildableId WaterExtractor = Id("Build_WaterPump_C_2147271168");
    public static readonly BuildableId Sink = Id("Build_ResourceSink_C_2147282188");
    public static readonly BuildableId FirstBelt = Id("Build_ConveyorBeltMk1_C_2147317296");
    public static readonly BuildableId Lift = Id("Build_ConveyorLiftMk1_C_2147312952");
    public static readonly BuildableId Splitter = Id("Build_ConveyorAttachmentSplitter_C_2147304077");
    public static readonly BuildableId SmartSplitter = Id("Build_ConveyorAttachmentSplitterSmart_C_2147286684");
    public static readonly BuildableId BeltIntoContainer = Id("Build_ConveyorBeltMk1_C_2147276358");
    public static readonly BuildableId Pipeline = Id("Build_Pipeline_C_2147257932");
    public static readonly BuildableId CleanPipeline = Id("Build_PipelineMK2_NoIndicator_C_2147257019");
    public static readonly BuildableId FreightPlatformUnloading = Id("Build_TrainDockingStation_C_2147427754");
    public static readonly BuildableId FreightPlatformLoading = Id("Build_TrainDockingStation_C_2147409101");
    public static readonly BuildableId DronePort = Id("Build_DroneStation_C_2147399058");
    public static readonly BuildableId TruckStationUnloading = Id("Build_TruckStation_C_2147469334");
    public static readonly BuildableId TruckStationLoading = Id("Build_TruckStation_C_2147458005");
    public static readonly BuildableId ScrewContainer = Id("Build_StorageContainerMk2_C_2147456833");

    public static T Get<T>(BuildableId id) where T : SavedBuildable =>
        Assert.IsType<T>(Assert.Single(Save.Buildables, buildable => buildable.Id == id));
}
