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

    public static readonly SavedWorld Saved = Read();

    /// <summary>Stored and read back, as the database does with it.</summary>
    public static SnapshotDocument Document { get; } =
        SnapshotDocument.FromJson(SnapshotDocument.Create(Saved, [IngotIron, IronPlate, IronRod, PackagedWater]).ToJson());

    public static string Id(string name) => $"Persistent_Level:PersistentLevel.{name}";

    public static readonly string BeltIntoContainer = Id("Build_ConveyorBeltMk1_C_2147276358");
    public static readonly string SmelterA = Id("Build_SmelterMk1_C_2147301226");
    public static readonly string SmelterB = Id("Build_SmelterMk1_C_2147301500");
    public static readonly string ConstructorC = Id("Build_ConstructorMk1_C_2147291547");

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
