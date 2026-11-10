using System.Text.Json;

using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.SaveFile;

namespace SatisfactoryLedger.World.Snapshots;

/// <param name="ClockPercent">The clock speed as the game shows it (33.3333), exact to its 4 decimals.</param>
/// <param name="Resource">The item a provider extracts when its class decides it (water); otherwise its node decides.</param>
/// <param name="Stock">What a station or container holds now, per item; null when the save says nothing about it.</param>
/// <param name="Burns">The fuel a drone port or truck station burns; null for buildables without a fuel slot.</param>
internal sealed record BuildableDocument(
    string Id,
    BuildableDocumentRole Role,
    string ClassName,
    string? Recipe = null,
    decimal? ClockPercent = null,
    decimal? ProductionBoost = null,
    string? Node = null,
    LogisticsKind? Kind = null,
    PositionDocument? Position = null,
    string? Resource = null,
    IReadOnlyDictionary<string, decimal>? Stock = null,
    IReadOnlyList<string>? Burns = null,
    IReadOnlyList<SortRuleDocument>? SortRules = null,
    IReadOnlyList<int>? InputPriorities = null,
    IReadOnlyList<CurvePointDocument>? Curve = null,
    double? Yaw = null,
    IReadOnlyDictionary<string, decimal>? Contents = null,
    IReadOnlyDictionary<string, decimal>? InputContents = null,
    IReadOnlyDictionary<string, decimal>? OutputContents = null,
    string? Fuel = null,
    bool TakesLeftoversOnly = false,
    DroneDocument? Drone = null)
{
    /// <param name="fluids">The fluids the Catalog knows: the save counts them in litres in a machine, the snapshot in cubic metres.</param>
    public static BuildableDocument From(SavedBuildable saved, IReadOnlySet<string> fluids) => FromKind(saved) with
    {
        Contents = (saved as SavedLogistics)?.Contents,
        InputContents = CubicMetres((saved as SavedMachine)?.InputContents ?? (saved as SavedConsumer)?.FuelContents, fluids),
        OutputContents = CubicMetres((saved as SavedMachine)?.OutputContents, fluids),
        Position = saved.Position is { } position ? new PositionDocument(position.X, position.Y, position.Z) : null,
        Yaw = Math.Round(saved.Yaw, 2),
        Stock = saved.Stock,
        Burns = saved.Burns?.Order(StringComparer.Ordinal).ToList(),
        Drone = saved.Drone is { } route ? new DroneDocument(route.Paired.Id, route.DistanceMetres, route.LatestTripSeconds) : null,
    };

    private static Dictionary<string, decimal>? CubicMetres(IReadOnlyDictionary<string, decimal>? contents, IReadOnlySet<string> fluids) =>
        contents?.ToDictionary(entry => entry.Key, entry => fluids.Contains(entry.Key) ? entry.Value / 1000 : entry.Value, StringComparer.Ordinal);

    private static BuildableDocument FromKind(SavedBuildable saved) => saved switch
    {
        SavedMachine machine => new(machine.Id.Id, BuildableDocumentRole.Machine, machine.ClassName,
            Recipe: machine.Recipe, ClockPercent: Percent(machine.ClockSpeed), ProductionBoost: machine.ProductionBoost),
        SavedProvider provider => new(provider.Id.Id, BuildableDocumentRole.Provider, provider.ClassName,
            ClockPercent: Percent(provider.ClockSpeed), Node: provider.Node, Resource: provider.Resource),
        SavedConsumer consumer => new(consumer.Id.Id, BuildableDocumentRole.Consumer, consumer.ClassName,
            ClockPercent: consumer.ClockSpeed is { } clock ? Percent(clock) : null, Fuel: consumer.Fuel,
            TakesLeftoversOnly: consumer.TakesLeftoversOnly),
        SavedLogistics logistics => new(logistics.Id.Id, BuildableDocumentRole.Logistics, logistics.ClassName, Kind: logistics.Kind,
            SortRules: logistics.SortRules?.Select(rule => new SortRuleDocument(rule.Output, rule.Kind, rule.Item?.Id)).ToList(),
            InputPriorities: logistics.InputPriorities,
            Curve: logistics.Curve?.Select(CurvePointDocument.From).ToList()),
        _ => throw new ArgumentException($"{saved} is not a kind of buildable a snapshot knows", nameof(saved)),
    };

    /// <param name="burnRate">For a drone port where drones refuel: what they burn there per minute, when known (BR-A16).</param>
    public Buildable ToBuildable(CopiedCatalog catalog, Rate? burnRate = null)
    {
        var id = BuildableId.Of(Id);
        return Role switch
        {
            // BR-W10: a machine whose recipe is unknown stays in the world, without known inputs or outputs.
            BuildableDocumentRole.Machine => new Machine(
                id,
                Recipe is not null && catalog.Recipes.TryGetValue(Recipe, out RecipeDocument? recipe) ? recipe.ToRecipe() : null,
                Clock(),
                ProductionBoost ?? 1)
            {
                InputStock = InputStockOf(),
                OutputStock = OutputContents?.ToDictionary(entry => ItemId.Of(entry.Key), entry => entry.Value),
            },
            BuildableDocumentRole.Provider => new Provider(id, OfferOf(catalog), StockOf(), BurnsOf(), burnRate)
            {
                Inventory = InventoryOf(catalog),
            },
            BuildableDocumentRole.Consumer => new Consumer(id, BurnsOf(), GenerationOf(catalog), TakesLeftoversOnly, burnRate)
            {
                InputStock = InputStockOf(),
                Inventory = TakesLeftoversOnly ? catalog.Depot : InventoryOf(catalog),
            },
            BuildableDocumentRole.Logistics => new Logistics(
                id,
                Kind ?? throw new JsonException($"Logistics {Id} has no kind"),
                catalog.TransportTiers.TryGetValue(ClassName, out TransportTierDocument? tier) ? Rate.PerMinute(tier.CapacityPerMinute) : null,
                StockOf(),
                SortRules?.Select(rule => new SortRule(rule.Output, rule.Kind, rule.Item is null ? null : ItemId.Of(rule.Item))).ToList(),
                InputPriorities)
            {
                Inventory = InventoryOf(catalog),
            },
            _ => throw new JsonException($"Buildable {Id} has an unknown role"),
        };
    }

    /// <summary>
    /// BR-A8: what a generator burning a known fuel takes and leaves per minute, at its clock speed. Null when it's no
    /// generator, its fuel slot was empty, or the Catalog has no entry for it or its fuel: it then takes everything.
    /// </summary>
    private Generation? GenerationOf(CopiedCatalog catalog)
    {
        if (Fuel is null || !catalog.Generators.TryGetValue(ClassName, out GeneratorDocument? generator)
            || generator.Fuels.FirstOrDefault(fuel => fuel.Fuel == Fuel) is not { } burnt)
        {
            return null;
        }

        ClockSpeed clock = Clock();
        Rate burns = Rate.FromRecipe(generator.PowerMegawatts, burnt.EnergyMegajoules) * clock;
        Rate water = generator.SupplementalLitresPerMegajoule > 0 && burnt.Supplemental is not null
            ? Rate.FromRecipe(generator.PowerMegawatts * generator.SupplementalLitresPerMegajoule, 1000) * clock
            : Rate.Zero;
        Rate leaves = burnt.Byproduct is not null && burnt.ByproductPerFuel > 0 ? burns * burnt.ByproductPerFuel : Rate.Zero;
        return new Generation(
            ItemId.Of(burnt.Fuel), burns,
            burnt.Supplemental is null ? null : ItemId.Of(burnt.Supplemental), water,
            burnt.Byproduct is null ? null : ItemId.Of(burnt.Byproduct), leaves);
    }

    /// <summary>
    /// BR-W9: what a provider offers, from its extractor's rate and its node's resource and purity. Null when the
    /// Catalog didn't know one of them: the analysis then assumes it supplies what is asked (BR-A9).
    /// </summary>
    private ItemOffer? OfferOf(CopiedCatalog catalog)
    {
        if (!catalog.Extractors.TryGetValue(ClassName, out ExtractorDocument? extractor))
        {
            return null;
        }

        (string? resource, Purity purity) = Resource is not null
            ? (Resource, Purity.Normal)
            : Node is not null && catalog.ResourceNodes.TryGetValue(Node, out ResourceNodeDocument? node)
                ? (node.Resource, Enum.Parse<Purity>(node.Purity, ignoreCase: true))
                : (null, Purity.Normal);

        return resource is null
            ? null
            : ItemOffer.Extracting(ItemId.Of(resource), Rate.FromRecipe(extractor.AmountPerCycle, extractor.CycleSeconds), purity, Clock());
    }

    /// <summary>BR-A17: what a machine or generator holds of its inputs; none in a snapshot older than its contents.</summary>
    private Dictionary<ItemId, decimal> InputStockOf() =>
        (InputContents ?? new Dictionary<string, decimal>()).ToDictionary(entry => ItemId.Of(entry.Key), entry => entry.Value);

    private HashSet<ItemId>? BurnsOf() => Burns?.Select(ItemId.Of).ToHashSet();

    private Dictionary<ItemId, decimal>? StockOf() => Stock?.ToDictionary(entry => ItemId.Of(entry.Key), entry => entry.Value);

    /// <summary>
    /// BR-A7: its inventory, when the Catalog knows the size of its class: slots, or a volume of fluid. Null otherwise,
    /// and it then takes everything, as before.
    /// </summary>
    private Inventory? InventoryOf(CopiedCatalog catalog)
    {
        if (!catalog.Storages.TryGetValue(ClassName, out StorageDocument? storage))
        {
            return null;
        }

        Dictionary<ItemId, decimal> holds = StockOf() ?? [];
        return storage.Slots > 0
            ? Inventory.OfSlots(storage.Slots, holds, catalog.StackSizes)
            : Inventory.OfFluid(storage.FluidCubicMetres, holds);
    }

    private ClockSpeed Clock() => ClockSpeed.Percent(ClockPercent ?? 100);

    /// <summary>A clock speed is a fraction of 1 (333333/1000000); stored as the percent the game shows.</summary>
    private static decimal Percent(ClockSpeed clockSpeed) => (decimal)clockSpeed.Numerator * 100 / (decimal)clockSpeed.Denominator;
}