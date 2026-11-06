using SatisfactoryLedger.Catalog.Contracts;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.SaveFile;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Tests;

/// <summary>The test world (Fixtures/Development.sav) as a stored snapshot, with the recipes it uses.</summary>
internal static class TestWorldSnapshot
{
    public static readonly CatalogRecipe IngotIron =
        new("Recipe_IngotIron_C", "Build_SmelterMk1_C", 2, [new("Desc_OreIron_C", 1)], [new("Desc_IronIngot_C", 1)]);

    public static readonly CatalogRecipe IronPlate =
        new("Recipe_IronPlate_C", "Build_ConstructorMk1_C", 6, [new("Desc_IronIngot_C", 3)], [new("Desc_IronPlate_C", 2)]);

    public static readonly CatalogRecipe IronRod =
        new("Recipe_IronRod_C", "Build_ConstructorMk1_C", 4, [new("Desc_IronIngot_C", 1)], [new("Desc_IronRod_C", 1)]);

    public static readonly CatalogRecipe PackagedWater =
        new("Recipe_PackagedWater_C", "Build_Packager_C", 2, [new("Desc_Water_C", 2), new("Desc_FluidCanister_C", 2)], [new("Desc_PackagedWater_C", 2)]);

    /// <summary>What the Catalog says, with values as the game's Docs file has them. The Mk.2 belt isn't in the test world.</summary>
    public static readonly CatalogCopy Catalog = new(
        [IngotIron, IronPlate, IronRod, PackagedWater],
        [new("Build_MinerMk1_C", 1, 1), new("Build_WaterPump_C", 2, 1)],
        [
            new("Build_ConveyorBeltMk1_C", 60), new("Build_ConveyorBeltMk2_C", 120), new("Build_ConveyorBeltMk6_C", 1200),
            new("Build_ConveyorLiftMk1_C", 60), new("Build_Pipeline_C", 300), new("Build_PipelineMK2_NoIndicator_C", 600),
        ],
        [new("BP_ResourceNode35", "resourceNode", "Desc_OreIron_C", "normal")],
        [
            new("Desc_OreIron_C", "Iron Ore", false), new("Desc_IronIngot_C", "Iron Ingot", false),
            new("Desc_Water_C", "Water", true), new("Desc_FluidCanister_C", "Empty Canister", false),
            new("Desc_LiquidOil_C", "Crude Oil", true),
        ]);

    public static readonly SavedWorld Saved = Read();

    /// <summary>Stored and read back, as the database does with it.</summary>
    public static SnapshotDocument Document { get; } = Stored(Catalog);

    /// <summary>The test world's analysis, stored and read back as the database does (ADR-0017).</summary>
    public static Simulation.AnalysisDocument TestWorldAnalysis { get; } =
        Simulation.AnalysisDocument.FromJson(Simulation.AnalysisDocument.Compute(Document.ToFlowNetwork()).ToJson());

    public static SnapshotDocument Stored(CatalogCopy catalog) => SnapshotDocument.FromJson(SnapshotDocument.Create(Saved, catalog).ToJson());

    public static string Id(string name) => $"Persistent_Level:PersistentLevel.{name}";

    public static readonly string BeltIntoContainer = Id("Build_ConveyorBeltMk1_C_2147276358");
    public static readonly string SmelterA = Id("Build_SmelterMk1_C_2147301226");
    public static readonly string SmelterB = Id("Build_SmelterMk1_C_2147301500");
    public static readonly string ConstructorC = Id("Build_ConstructorMk1_C_2147291547");
    public static readonly string Miner = Id("Build_MinerMk1_C_2147339296");
    public static readonly string WaterExtractor = Id("Build_WaterPump_C_2147271168");
    public static readonly string Packager = Id("Build_Packager_C_2147260704");

    private static SavedWorld Read()
    {
        using FileStream file = File.OpenRead("Fixtures/Development.sav");
        return SaveFileReader.Read(file);
    }

    /// <summary>The same snapshot with one buildable taken out, as if it were rebuilt or not built yet.</summary>
    public static SnapshotDocument Without(string id) => Document with
    {
        Buildables = [.. Document.Buildables.Where(buildable => buildable.Id != id)],
        Connections = [.. Document.Connections.Where(connection => connection.From != id && connection.To != id)],
    };

    public static ItemId Item(string id) => ItemId.Of(id);
}
