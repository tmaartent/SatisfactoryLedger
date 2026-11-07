using System.Text.Json;
using System.Text.Json.Serialization;

using SatisfactoryLedger.Catalog.Contracts;
using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.SaveFile;

namespace SatisfactoryLedger.World.Snapshots;

internal sealed record SnapshotCounts(int Machines, int Providers, int Consumers, int Logistics, int Connections);

/// <summary>What the Catalog said, at import, about the recipes, extractors, belts, nodes, items and footprints a save uses.</summary>
internal sealed record CatalogCopy(
    IReadOnlyCollection<CatalogRecipe> Recipes,
    IReadOnlyCollection<CatalogExtractor> Extractors,
    IReadOnlyCollection<CatalogTransportTier> TransportTiers,
    IReadOnlyCollection<CatalogResourceNode> ResourceNodes,
    IReadOnlyCollection<CatalogItem> Items,
    IReadOnlyCollection<CatalogFootprint> Footprints,
    IReadOnlyCollection<CatalogGenerator> Generators,
    IReadOnlyCollection<CatalogDronePort>? DronePorts = null);

/// <summary>
/// The stored content of a world snapshot (ADR-0016): everything the save said, plus what the Catalog said about the
/// recipes, extractors, belts and nodes it uses, at import. A plain document of our own, so the domain model stays
/// free of persistence.
/// </summary>
internal sealed record SnapshotDocument(
    int Version,
    IReadOnlyList<BuildableDocument> Buildables,
    IReadOnlyList<ConnectionDocument> Connections,
    IReadOnlyList<RecipeDocument> Recipes,
    IReadOnlyList<string> UnknownRecipes,
    IReadOnlyList<UnsupportedDocument> Unsupported,
    IReadOnlyList<ExtractorDocument> Extractors,
    IReadOnlyList<TransportTierDocument> TransportTiers,
    IReadOnlyList<ResourceNodeDocument> ResourceNodes,
    IReadOnlyList<string> Fluids,
    IReadOnlyList<FootprintDocument>? Footprints = null,
    IReadOnlyList<GeneratorDocument>? Generators = null,
    IReadOnlyList<DronePortDocument>? DronePorts = null)
{
    /// <summary>
    /// Version 2 added extractors, transport tiers, resource nodes and fluids; a version 1 snapshot has none of them.
    /// Version 3 added the stock of stations and containers; a version 2 snapshot has none.
    /// Version 4 added splitter rules, merger priorities and the ports of connections; a version 3 snapshot has none.
    /// Version 5 added the curves of belts and pipes, which way buildables face, and the footprints of the buildings it
    /// has (ADR-0020); a version 4 snapshot has none of them.
    /// Version 6 added contents: what's on belts, lifts, splitters and mergers, and in machines; a version 5 snapshot has none.
    /// Version 7 added generators: the fuel each burns, its clock speed, and the Catalog's generators (BR-A8); in a
    /// version 6 snapshot every generator takes everything, as before.
    /// Version 8 added drones: each home port's route, and the Catalog's drone port (BR-A16); in a version 7 snapshot
    /// every drone port takes its fuel from what's left, as before.
    /// Version 9 made each pipe network one hub (BR-A6); a version 8 snapshot keeps the pipe directions guessed at import.
    /// </summary>
    public const int CurrentVersion = 9;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>A snapshot of a save, with what <paramref name="catalog"/> says about what it uses. Unknown recipes are listed (BR-W10).</summary>
    public static SnapshotDocument Create(SavedWorld saved, CatalogCopy catalog)
    {
        HashSet<string> used = [.. saved.Buildables.OfType<SavedMachine>().Select(machine => machine.Recipe).OfType<string>()];
        List<RecipeDocument> known = [.. catalog.Recipes.Where(recipe => used.Contains(recipe.Id)).Select(RecipeDocument.From)];
        HashSet<string> knownIds = [.. known.Select(recipe => recipe.Id)];

        HashSet<string> classes = [.. saved.Buildables.Select(buildable => buildable.ClassName)];
        HashSet<string> nodes = [.. saved.Buildables.OfType<SavedProvider>().Select(provider => provider.Node).OfType<string>()];
        List<ResourceNodeDocument> nodesUsed = [.. catalog.ResourceNodes.Where(node => nodes.Contains(node.Name))
            .Select(node => new ResourceNodeDocument(node.Name, node.Resource, node.Purity))];
        List<GeneratorDocument> generators = [.. catalog.Generators.Where(generator => classes.Contains(generator.Id))
            .OrderBy(generator => generator.Id, StringComparer.Ordinal).Select(GeneratorDocument.From)];
        HashSet<string> items = ItemsUsed(saved, known, nodesUsed, generators);
        HashSet<string> fluids = [.. catalog.Items.Where(item => item.IsFluid).Select(item => item.Id)];
        HashSet<string> fuelsHeld = [.. saved.Buildables.SelectMany(buildable => buildable.Burns ?? new HashSet<string>())];
        List<DronePortDocument> dronePorts = [.. (catalog.DronePorts ?? []).Where(port => classes.Contains(port.Id))
            .OrderBy(port => port.Id, StringComparer.Ordinal).Select(port => DronePortDocument.From(port, fuelsHeld))];

        return new SnapshotDocument(
            CurrentVersion,
            [.. saved.Buildables.Select(buildable => BuildableDocument.From(buildable, fluids))],
            [.. saved.Connections.Select(connection => new ConnectionDocument(connection.From.Id, connection.To.Id, connection.FromPort, connection.ToPort))],
            known,
            [.. used.Where(id => !knownIds.Contains(id)).Order(StringComparer.Ordinal)],
            [.. saved.Unsupported.Select(unsupported => new UnsupportedDocument(unsupported.ClassName, unsupported.Count))],
            [.. catalog.Extractors.Where(extractor => classes.Contains(extractor.Id))
                .Select(extractor => new ExtractorDocument(extractor.Id, extractor.AmountPerCycle, extractor.CycleSeconds))],
            [.. catalog.TransportTiers.Where(tier => classes.Contains(tier.Id))
                .Select(tier => new TransportTierDocument(tier.Id, tier.CapacityPerMinute))],
            nodesUsed,
            [.. catalog.Items.Where(item => item.IsFluid && items.Contains(item.Id)).Select(item => item.Id).Order(StringComparer.Ordinal)],
            [.. catalog.Footprints.Where(footprint => classes.Contains(footprint.Id)).OrderBy(footprint => footprint.Id, StringComparer.Ordinal)
                .Select(footprint => new FootprintDocument(footprint.Id, footprint.MinX, footprint.MinY, footprint.MaxX, footprint.MaxY))],
            generators,
            dronePorts);
    }

    /// <summary>The items a save's recipes take and make, and its providers extract.</summary>
    public static HashSet<string> ItemsUsed(
        SavedWorld saved, IEnumerable<RecipeDocument> recipes, IEnumerable<ResourceNodeDocument> nodes, IEnumerable<GeneratorDocument> generators) =>
    [
        .. recipes.SelectMany(recipe => recipe.Inputs.Concat(recipe.Outputs)).Select(ingredient => ingredient.Item),
        .. saved.Buildables.OfType<SavedProvider>().Select(provider => provider.Resource).OfType<string>(),
        .. nodes.Select(node => node.Resource),
        // A generator's fuel, water and waste: water and fluid fuels must be known as fluids for the pipes (step 14).
        .. generators.SelectMany(generator => generator.Fuels)
            .SelectMany(fuel => new[] { fuel.Fuel, fuel.Supplemental, fuel.Byproduct }).OfType<string>(),
    ];

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static SnapshotDocument FromJson(string json)
    {
        SnapshotDocument document = JsonSerializer.Deserialize<SnapshotDocument>(json, Json)
            ?? throw new JsonException("A snapshot document can't be empty");

        return document.Version switch
        {
            CurrentVersion => document,
            // Imported before stock, splitter rules and ports, or curves were read: everything else is the same.
            2 or 3 or 4 or 5 or 6 or 7 or 8 => document with { Version = CurrentVersion },
            // Imported before rates were copied: it traces as before, but the flow analysis knows no rates.
            1 => document with { Version = CurrentVersion, Extractors = [], TransportTiers = [], ResourceNodes = [], Fluids = [] },
            _ => throw new NotSupportedException($"Snapshot document version {document.Version} is not supported"),
        };
    }

    public SnapshotCounts Counts() => new(
        Buildables.Count(buildable => buildable.Role == BuildableDocumentRole.Machine),
        Buildables.Count(buildable => buildable.Role == BuildableDocumentRole.Provider),
        Buildables.Count(buildable => buildable.Role == BuildableDocumentRole.Consumer),
        // A pipe network's hub isn't built by the player (BR-A6): not counted.
        Buildables.Count(buildable => buildable.Role == BuildableDocumentRole.Logistics && buildable.Kind != LogisticsKind.PipeNetwork),
        Connections.Count);

    /// <summary>The world as a network the flow analysis can run on.</summary>
    public FlowNetwork ToFlowNetwork() => WorldFlow.From(ToWorldGraph(), Fluids.Select(ItemId.Of));

    /// <summary>The world as a graph the domain can trace and analyse, with the rates the Catalog gave at import.</summary>
    public WorldGraph ToWorldGraph()
    {
        var copied = new CopiedCatalog(
            Recipes.ToDictionary(recipe => recipe.Id, StringComparer.Ordinal),
            Extractors.ToDictionary(extractor => extractor.Id, StringComparer.Ordinal),
            TransportTiers.ToDictionary(tier => tier.Id, StringComparer.Ordinal),
            ResourceNodes.ToDictionary(node => node.Name, StringComparer.Ordinal),
            (Generators ?? []).ToDictionary(generator => generator.Id, StringComparer.Ordinal));

        IReadOnlyDictionary<BuildableId, Rate> burnRates = DroneBurnRates();
        return new WorldGraph(
            Buildables.Select(buildable => buildable.ToBuildable(copied, burnRates.TryGetValue(BuildableId.Of(buildable.Id), out Rate? rate) ? rate : null)),
            Connections.Select(connection => new Connection(BuildableId.Of(connection.From), BuildableId.Of(connection.To), connection.FromPort, connection.ToPort)));
    }

    /// <summary>BR-A16: per drone port where drones refuel, what they burn there per minute, when that's known.</summary>
    private IReadOnlyDictionary<BuildableId, Rate> DroneBurnRates()
    {
        Dictionary<string, DronePortDocument> ports = (DronePorts ?? []).ToDictionary(port => port.Id, StringComparer.Ordinal);
        List<BuildableDocument> homes = [.. Buildables.Where(buildable => buildable.Drone is not null && ports.ContainsKey(buildable.ClassName))];
        if (homes.Count == 0)
        {
            return new Dictionary<BuildableId, Rate>();
        }

        // There is one kind of drone port; every port of it knows the same fuels.
        DronePortDocument port = ports[homes[0].ClassName];
        Dictionary<BuildableId, ItemId> fuelInSlot = Buildables
            .Where(buildable => ports.ContainsKey(buildable.ClassName) && buildable.Burns is { Count: > 0 })
            .ToDictionary(buildable => BuildableId.Of(buildable.Id), buildable => ItemId.Of(buildable.Burns![0]));

        return DroneRefuelling.BurnRates(
            homes.Select(home => new Drone(
                BuildableId.Of(home.Id),
                BuildableId.Of(home.Drone!.Paired),
                home.Drone.LatestTripSeconds is { } seconds ? new DroneRoute(home.Drone.DistanceMetres, seconds) : null)),
            fuelInSlot,
            new DroneTripCost(port.TripEnergyMegajoules, port.TripEnergyPerMetreMegajoules),
            fuel => port.Fuels.TryGetValue(fuel.Id, out decimal energy) ? energy : null);
    }
}

/// <summary>The Catalog copy of a snapshot, by id, to look up while building the world.</summary>
internal sealed record CopiedCatalog(
    IReadOnlyDictionary<string, RecipeDocument> Recipes,
    IReadOnlyDictionary<string, ExtractorDocument> Extractors,
    IReadOnlyDictionary<string, TransportTierDocument> TransportTiers,
    IReadOnlyDictionary<string, ResourceNodeDocument> ResourceNodes,
    IReadOnlyDictionary<string, GeneratorDocument> Generators);

internal enum BuildableDocumentRole
{
    Machine,
    Provider,
    Consumer,
    Logistics,
}

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
            },
            BuildableDocumentRole.Provider => new Provider(id, OfferOf(catalog), StockOf(), BurnsOf(), burnRate),
            BuildableDocumentRole.Consumer => new Consumer(id, BurnsOf(), GenerationOf(catalog), TakesLeftoversOnly, burnRate)
            {
                InputStock = InputStockOf(),
            },
            BuildableDocumentRole.Logistics => new Logistics(
                id,
                Kind ?? throw new JsonException($"Logistics {Id} has no kind"),
                catalog.TransportTiers.TryGetValue(ClassName, out TransportTierDocument? tier) ? Rate.PerMinute(tier.CapacityPerMinute) : null,
                StockOf(),
                SortRules?.Select(rule => new SortRule(rule.Output, rule.Kind, rule.Item is null ? null : ItemId.Of(rule.Item))).ToList(),
                InputPriorities),
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

    private ClockSpeed Clock() => ClockSpeed.Percent(ClockPercent ?? 100);

    /// <summary>A clock speed is a fraction of 1 (333333/1000000); stored as the percent the game shows.</summary>
    private static decimal Percent(ClockSpeed clockSpeed) => (decimal)clockSpeed.Numerator * 100 / (decimal)clockSpeed.Denominator;
}

/// <summary>Where a buildable stands, in the game's units (centimetres).</summary>
internal sealed record PositionDocument(double X, double Y, double Z)
{
    public static PositionDocument From(SavedPosition position) => new(position.X, position.Y, position.Z);
}

/// <summary>A point of a belt's or pipe's curve, with the tangents it arrives and leaves with (world coordinates, centimetres).</summary>
internal sealed record CurvePointDocument(PositionDocument Location, PositionDocument ArriveTangent, PositionDocument LeaveTangent)
{
    /// <summary>Rounded to a hundredth of a centimetre: finer than anything the map shows, and a third of the size.</summary>
    public static CurvePointDocument From(SavedCurvePoint point) =>
        new(Rounded(point.Location), Rounded(point.ArriveTangent), Rounded(point.LeaveTangent));

    private static PositionDocument Rounded(SavedPosition position) =>
        new(Math.Round(position.X, 2), Math.Round(position.Y, 2), Math.Round(position.Z, 2));
}

/// <param name="FromPort">The splitter output it leaves from; null where that doesn't matter.</param>
/// <param name="ToPort">The merger input it enters by; null where that doesn't matter.</param>
internal sealed record ConnectionDocument(string From, string To, int? FromPort = null, int? ToPort = null);

/// <param name="Item">The item's class name, for a rule of kind <c>item</c>.</param>
internal sealed record SortRuleDocument(int Output, SortRuleKind Kind, string? Item = null);

internal sealed record RecipeDocument(string Id, decimal CycleSeconds, IReadOnlyList<IngredientDocument> Inputs, IReadOnlyList<IngredientDocument> Outputs)
{
    public static RecipeDocument From(CatalogRecipe recipe) => new(
        recipe.Id,
        recipe.CycleSeconds,
        [.. recipe.Inputs.Select(input => new IngredientDocument(input.ItemId, input.AmountPerCycle))],
        [.. recipe.Outputs.Select(output => new IngredientDocument(output.ItemId, output.AmountPerCycle))]);

    /// <summary>Per minute at 100%. An item that appears twice on one side counts once, with both amounts.</summary>
    public Recipe ToRecipe() => new(PerMinute(Inputs), PerMinute(Outputs));

    private Dictionary<ItemId, Rate> PerMinute(IEnumerable<IngredientDocument> ingredients) => ingredients
        .GroupBy(ingredient => ingredient.Item, StringComparer.Ordinal)
        .ToDictionary(group => ItemId.Of(group.Key), group => Rate.FromRecipe(group.Sum(ingredient => ingredient.AmountPerCycle), CycleSeconds));
}

internal sealed record IngredientDocument(string Item, decimal AmountPerCycle);

internal sealed record UnsupportedDocument(string ClassName, int Count);

/// <param name="Id">The building's class name, e.g. <c>Build_MinerMk1_C</c>.</param>
internal sealed record ExtractorDocument(string Id, decimal AmountPerCycle, decimal CycleSeconds);

/// <param name="Id">The building's class name, e.g. <c>Build_ConveyorBeltMk1_C</c>.</param>
internal sealed record TransportTierDocument(string Id, int CapacityPerMinute);

/// <param name="Purity"><c>impure</c>, <c>normal</c> or <c>pure</c>, as the Catalog writes it.</param>
internal sealed record ResourceNodeDocument(string Name, string Resource, string Purity);

/// <summary>The ground a building class covers, seen from above, in its own coordinates (centimetres), as the Catalog said at import.</summary>
/// <summary>A generator as the Catalog said at import (BR-A8): power, litres of water per MJ, and the fuels it burns.</summary>
internal sealed record GeneratorDocument(string Id, decimal PowerMegawatts, decimal SupplementalLitresPerMegajoule, IReadOnlyList<GeneratorFuelDocument> Fuels)
{
    public static GeneratorDocument From(CatalogGenerator generator) => new(
        generator.Id, generator.PowerMegawatts, generator.SupplementalLitresPerMegajoule,
        [.. generator.Fuels.Select(fuel => new GeneratorFuelDocument(fuel.Fuel, fuel.EnergyMegajoules, fuel.Supplemental, fuel.Byproduct, fuel.ByproductPerFuel))]);
}

/// <param name="EnergyMegajoules">Per item, or per cubic metre of a fluid fuel.</param>
internal sealed record GeneratorFuelDocument(string Fuel, decimal EnergyMegajoules, string? Supplemental, string? Byproduct, decimal ByproductPerFuel);

/// <summary>A drone's route, kept on its home port (BR-A16).</summary>
/// <param name="Paired">The id of the port it flies to.</param>
/// <param name="DistanceMetres">To that port, measured flat.</param>
/// <param name="LatestTripSeconds">How long its latest round trip took; null when it hasn't made one yet.</param>
internal sealed record DroneDocument(string Paired, decimal DistanceMetres, decimal? LatestTripSeconds);

/// <summary>The drone port as the Catalog said at import (BR-A16): what a trip costs, and the energy of the fuels its ports hold.</summary>
/// <param name="Fuels">Per fuel, megajoules per item.</param>
internal sealed record DronePortDocument(
    string Id, decimal TripEnergyMegajoules, decimal TripEnergyPerMetreMegajoules, IReadOnlyDictionary<string, decimal> Fuels)
{
    /// <param name="fuelsHeld">The fuels in the save's fuel slots: only theirs are kept.</param>
    public static DronePortDocument From(CatalogDronePort port, IReadOnlySet<string> fuelsHeld) => new(
        port.Id, port.TripEnergyMegajoules, port.TripEnergyPerMetreMegajoules,
        port.Fuels.Where(fuel => fuelsHeld.Contains(fuel.Fuel)).OrderBy(fuel => fuel.Fuel, StringComparer.Ordinal)
            .ToDictionary(fuel => fuel.Fuel, fuel => fuel.EnergyMegajoules, StringComparer.Ordinal));
}

internal sealed record FootprintDocument(string Id, decimal MinX, decimal MinY, decimal MaxX, decimal MaxY);
