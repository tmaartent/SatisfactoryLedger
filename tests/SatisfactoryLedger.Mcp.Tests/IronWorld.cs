using System.Net;

namespace SatisfactoryLedger.Mcp.Tests;

/// <summary>
/// A small made-up world, as the API would answer about it: iron ore to ingots to plates. Two snapshots, the newest
/// analysed: two plate constructors starved of ingots, one blocked, one without a recipe, one smelter at full speed.
/// </summary>
internal static class IronWorld
{
    public const string Newest = "11111111-1111-1111-1111-111111111111";
    public const string Older = "22222222-2222-2222-2222-222222222222";
    public const string PlateFactory = "33333333-3333-3333-3333-333333333333";

    public static FakeLedgerApi Api()
    {
        var api = new FakeLedgerApi();
        Catalog(api);
        api.Answer("/api/v1/world/snapshots", new[]
        {
            Snapshot(Newest, "2026-11-20T09:00:00Z", "ready"),
            Snapshot(Older, "2026-11-19T09:00:00Z", "ready"),
        });
        api.Answer($"/api/v1/world/snapshots/{Newest}/analysis", Analysis());
        api.Answer($"/api/v1/world/snapshots/{Newest}/factories", new[]
        {
            new
            {
                factoryId = PlateFactory, name = "Plates", traceable = true, logistics = 12,
                machines = new[] { PlateMachine("m-1"), PlateMachine("m-2"), PlateMachine("m-4") },
                outputs = Array.Empty<object>(), inputs = Array.Empty<object>(), missing = Array.Empty<string>(),
            },
        });
        api.Answer($"/api/v1/world/snapshots/{Newest}/factories/{PlateFactory}/analysis", new
        {
            factoryId = PlateFactory,
            name = "Plates",
            settled = true,
            summary = new { machines = 3, running = 0, starved = 2, outputBlocked = 1, noRecipe = 0 },
            generatorSummary = new { generators = 0, running = 0, starved = 0, outputBlocked = 0, fuelUnknown = 0 },
            output = new[]
            {
                new { item = "Desc_IronPlate_C", now = 20.0, atFullSpeed = 60.0, receiversNeed = 0.0, othersBring = 0.0, @short = 0.0, sunk = 0.0, cannotLeave = 4.0, destinations = Array.Empty<object>() },
            },
            input = new[]
            {
                new { item = "Desc_IronIngot_C", now = 30.0, atFullSpeed = 90.0, @short = 60.0, sources = Array.Empty<object>() },
            },
            power = new { nowMegawatts = 6.0, atFullSpeedMegawatts = 12.0 },
            machines = new[] { Starved("m-1"), Starved("m-2"), Blocked("m-4") },
            generators = Array.Empty<object>(),
            bottlenecks = Array.Empty<object>(),
            unusedSupply = Array.Empty<object>(),
            unknownSupply = Array.Empty<object>(),
        });
        api.Answer($"/api/v1/world/snapshots/{Newest}/traces/shortage?building=Build_ConstructorMk1_C&recipe=Recipe_IronPlate_C", new
        {
            building = "Build_ConstructorMk1_C",
            recipe = "Recipe_IronPlate_C",
            machines = 3,
            traced = 2,
            starts = new[]
            {
                new
                {
                    cause = "tooFewProducers",
                    item = "Desc_IronIngot_C",
                    at = new[] { Group("Build_SmelterMk1_C", "Recipe_IngotIron_C", 1, 0, "m-3") },
                    path = new[] { new { machines = new[] { Group("Build_ConstructorMk1_C", "Recipe_IronPlate_C", 2, 0, "m-1", "m-2") }, item = "Desc_IronIngot_C", perMinute = 30.0 } },
                    perMinute = 30.0,
                    others = Array.Empty<object>(),
                    shortOf = new Dictionary<string, double>(),
                    moreCapacity = (object?)null,
                },
            },
        });
        api.Answer($"/api/v1/world/snapshots/{Newest}/traces/shortage?building=Build_SmelterMk1_C", new
        {
            building = "Build_SmelterMk1_C", recipe = (string?)null, machines = 1, traced = 0, starts = Array.Empty<object>(),
        });
        api.Answer($"/api/v1/world/snapshots/{Newest}/traces/blockage?building=Build_SmelterMk1_C", new
        {
            building = "Build_SmelterMk1_C",
            recipe = (string?)null,
            machines = 1,
            traced = 1,
            starts = new[]
            {
                new
                {
                    cause = "beltFull",
                    item = "Desc_IronIngot_C",
                    at = new[] { Group("Build_ConveyorBeltMk3_C", null, 1, 0, "belt-7") },
                    path = new[] { new { machines = new[] { Group("Build_SmelterMk1_C", "Recipe_IngotIron_C", 1, 0, "m-3") }, item = "Desc_IronIngot_C", perMinute = 30.0 } },
                    perMinute = 270.0,
                    others = Array.Empty<object>(),
                    shortOf = new Dictionary<string, double>(),
                    moreCapacity = new { tier = "Build_ConveyorBeltMk4_C", lines = 1, carries = 300.0 },
                },
            },
        });
        api.Answer($"/api/v1/world/snapshots/{Newest}/traces/blockage?building=Build_ConstructorMk1_C&recipe=Recipe_IronPlate_C", new
        {
            building = "Build_ConstructorMk1_C",
            recipe = "Recipe_IronPlate_C",
            machines = 3,
            traced = 1,
            starts = new[]
            {
                new
                {
                    cause = "suppliedByOthers",
                    item = "Desc_IronPlate_C",
                    at = new[] { Group("Build_ConstructorMk1_C", "Recipe_IronRod_C", 1, 0, "m-9") },
                    path = new[] { new { machines = new[] { Group("Build_ConstructorMk1_C", "Recipe_IronPlate_C", 1, 0, "m-4") }, item = "Desc_IronPlate_C", perMinute = 4.0 } },
                    perMinute = 15.0,
                    others = new[] { Group("Build_SmelterMk1_C", "Recipe_IngotIron_C", 1, 10, "m-3") },
                    shortOf = new Dictionary<string, double>(),
                    moreCapacity = (object?)null,
                },
            },
        });
        return api;
    }

    /// <summary>The newest snapshot still being analysed: the API answers 202 with how far it is.</summary>
    public static FakeLedgerApi Analysing()
    {
        FakeLedgerApi api = Api();
        api.Answer($"/api/v1/world/snapshots/{Newest}/analysis", new { status = "pending" }, HttpStatusCode.Accepted);
        return api;
    }

    private static void Catalog(FakeLedgerApi api)
    {
        api.Answer("/api/v1/catalog/items", new[]
        {
            Item("Desc_OreIron_C", "Iron Ore", raw: true),
            Item("Desc_IronIngot_C", "Iron Ingot"),
            Item("Desc_IronPlate_C", "Iron Plate"),
            Item("Desc_IronRod_C", "Iron Rod"),
            Item("Desc_Water_C", "Water", form: "fluid", raw: true),
        });
        api.Answer("/api/v1/catalog/buildables", new[]
        {
            new { id = "Build_SmelterMk1_C", name = "Smelter", inGame = true },
            new { id = "Build_ConstructorMk1_C", name = "Constructor", inGame = true },
            new { id = "Build_ConveyorBeltMk3_C", name = "Conveyor Belt Mk.3", inGame = true },
            new { id = "Build_ConveyorBeltMk4_C", name = "Conveyor Belt Mk.4", inGame = true },
            new { id = "Build_MinerMk2_C", name = "Miner Mk.2", inGame = true },
        });
        object[] recipes =
        [
            Recipe("Recipe_IngotIron_C", "Iron Ingot", "Build_SmelterMk1_C", false, [("Desc_OreIron_C", 30)], [("Desc_IronIngot_C", 30)]),
            Recipe("Recipe_IronPlate_C", "Iron Plate", "Build_ConstructorMk1_C", false, [("Desc_IronIngot_C", 30)], [("Desc_IronPlate_C", 20)]),
            Recipe("Recipe_Alternate_CoatedIronPlate_C", "Alternate: Coated Iron Plate", "Build_ConstructorMk1_C", true, [("Desc_IronIngot_C", 37.5)], [("Desc_IronPlate_C", 75)]),
            Recipe("Recipe_IronRod_C", "Iron Rod", "Build_ConstructorMk1_C", false, [("Desc_IronIngot_C", 15)], [("Desc_IronRod_C", 15)]),
        ];
        api.Answer("/api/v1/catalog/recipes", recipes);
        api.Answer("/api/v1/catalog/recipes?produces=Desc_IronPlate_C", recipes[1..3]);
        api.Answer("/api/v1/catalog/transport-tiers", new[]
        {
            new { id = "Build_ConveyorBeltMk3_C", name = "Conveyor Belt Mk.3", form = "belt", capacity = 270, inGame = true },
            new { id = "Build_PipelineMK2_C", name = "Pipeline Mk.2", form = "pipe", capacity = 600, inGame = true },
        });
    }

    /// <summary>A plate constructor of the factory, as its trace names it.</summary>
    private static object PlateMachine(string id) =>
        new { id, className = "Build_ConstructorMk1_C", recipe = "Recipe_IronPlate_C", owner = PlateFactory };

    private static object Snapshot(string id, string savedAt, string analysis) => new
    {
        id,
        sessionName = "Iron Valley",
        gameBuild = 400000,
        savedAt,
        importedAt = savedAt,
        counts = new { machines = 5, providers = 2, consumers = 1, logistics = 20, connections = 24 },
        analysis,
    };

    private static object Analysis() => new
    {
        settled = true,
        summary = new { machines = 5, running = 1, starved = 2, outputBlocked = 1, noRecipe = 1 },
        generatorSummary = new { generators = 0, running = 0, starved = 0, outputBlocked = 0, fuelUnknown = 0 },
        machines = new[]
        {
            Starved("m-1"),
            Starved("m-2"),
            Machine("m-3", "Build_SmelterMk1_C", "Recipe_IngotIron_C", 1.0, "none"),
            Blocked("m-4"),
            Machine("m-5", "Build_ConstructorMk1_C", null, 0.0, "noRecipe"),
        },
        generators = Array.Empty<object>(),
        dronePorts = Array.Empty<object>(),
        pipeFlows = Array.Empty<object>(),
        bottlenecks = new[]
        {
            new { id = "belt-7", className = "Build_ConveyorBeltMk3_C", capacity = 270.0, flow = 270.0, offeredMore = 30.0, position = new { x = 100.0, y = 200.0, z = 0.0 }, approximate = false },
        },
        unusedSupply = new[]
        {
            new { id = "miner-1", className = "Build_MinerMk2_C", item = "Desc_OreIron_C", offered = 120.0, delivered = 60.0, position = new { x = 0.0, y = 0.0, z = 0.0 }, approximate = false },
        },
        unknownSupply = Array.Empty<object>(),
        approximateParts = Array.Empty<object>(),
    };

    private static object Starved(string id) =>
        Machine(id, "Build_ConstructorMk1_C", "Recipe_IronPlate_C", 0.5, "starved", "Desc_IronIngot_C", starvedOf: new Dictionary<string, double> { ["Desc_IronIngot_C"] = 0.5 });

    private static object Blocked(string id) =>
        Machine(id, "Build_ConstructorMk1_C", "Recipe_IronPlate_C", 0.8, "outputBlocked", "Desc_IronPlate_C", blockedOn: new Dictionary<string, double> { ["Desc_IronPlate_C"] = 0.8 });

    private static object Machine(
        string id, string className, string? recipe, double utilisation, string limit, string? limitingItem = null,
        Dictionary<string, double>? starvedOf = null, Dictionary<string, double>? blockedOn = null) => new
        {
            id,
            className,
            recipe,
            utilisation,
            limit,
            limitingItem,
            position = new { x = 1250.0, y = -300.0, z = 400.0 },
            approximate = false,
            starvedOf = starvedOf ?? [],
            blockedOn = blockedOn ?? [],
            onStock = (string?)null,
            stockMinutes = (double?)null,
        };

    private static object Group(string className, string? recipe, int count, double perMinute, params string[] ids) =>
        new { className, recipe, count, perMinute, ids, position = new { x = 1250.0, y = -300.0, z = 400.0 } };

    private static object Item(string id, string name, string form = "solid", bool raw = false) =>
        new { id, name, form, isRawResource = raw, inGame = true, fluidColour = (string?)null };

    private static object Recipe(string id, string name, string building, bool alternate, (string Item, double PerMinute)[] inputs, (string Item, double PerMinute)[] outputs) => new
    {
        id,
        name,
        buildingId = building,
        cycleSeconds = 6.0,
        isAlternate = alternate,
        inGame = true,
        inputs = inputs.Select(Ingredient),
        outputs = outputs.Select(Ingredient),
    };

    private static object Ingredient((string Item, double PerMinute) ingredient) =>
        new { itemId = ingredient.Item, amountPerCycle = ingredient.PerMinute / 10, rate = new { perMinute = ingredient.PerMinute, exact = $"{ingredient.PerMinute}" } };
}
