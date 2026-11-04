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

    public static T Get<T>(BuildableId id) where T : SavedBuildable =>
        Assert.IsType<T>(Assert.Single(Save.Buildables, buildable => buildable.Id == id));
}
