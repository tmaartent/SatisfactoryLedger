using System.Text.Json;
using System.Text.Json.Serialization;

using SatisfactoryLedger.Catalog.Contracts;
using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.SaveFile;

namespace SatisfactoryLedger.World.Snapshots;

internal sealed record SnapshotCounts(int Machines, int Providers, int Consumers, int Logistics, int Connections);

/// <summary>What the Catalog said, at import, about the recipes, extractors, belts, nodes and items a save uses.</summary>
internal sealed record CatalogCopy(
    IReadOnlyCollection<CatalogRecipe> Recipes,
    IReadOnlyCollection<CatalogExtractor> Extractors,
    IReadOnlyCollection<CatalogTransportTier> TransportTiers,
    IReadOnlyCollection<CatalogResourceNode> ResourceNodes,
    IReadOnlyCollection<CatalogItem> Items);

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
    IReadOnlyList<string> Fluids)
{
    /// <summary>
    /// Version 2 added extractors, transport tiers, resource nodes and fluids; a version 1 snapshot has none of them.
    /// Version 3 added the stock of stations and containers; a version 2 snapshot has none.
    /// Version 4 added splitter rules, merger priorities and the ports of connections; a version 3 snapshot has none.
    /// </summary>
    public const int CurrentVersion = 4;

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
        HashSet<string> items = ItemsUsed(saved, known, nodesUsed);

        return new SnapshotDocument(
            CurrentVersion,
            [.. saved.Buildables.Select(BuildableDocument.From)],
            [.. saved.Connections.Select(connection => new ConnectionDocument(connection.From.Id, connection.To.Id, connection.FromPort, connection.ToPort))],
            known,
            [.. used.Where(id => !knownIds.Contains(id)).Order(StringComparer.Ordinal)],
            [.. saved.Unsupported.Select(unsupported => new UnsupportedDocument(unsupported.ClassName, unsupported.Count))],
            [.. catalog.Extractors.Where(extractor => classes.Contains(extractor.Id))
                .Select(extractor => new ExtractorDocument(extractor.Id, extractor.AmountPerCycle, extractor.CycleSeconds))],
            [.. catalog.TransportTiers.Where(tier => classes.Contains(tier.Id))
                .Select(tier => new TransportTierDocument(tier.Id, tier.CapacityPerMinute))],
            nodesUsed,
            [.. catalog.Items.Where(item => item.IsFluid && items.Contains(item.Id)).Select(item => item.Id).Order(StringComparer.Ordinal)]);
    }

    /// <summary>The items a save's recipes take and make, and its providers extract.</summary>
    public static HashSet<string> ItemsUsed(SavedWorld saved, IEnumerable<RecipeDocument> recipes, IEnumerable<ResourceNodeDocument> nodes) =>
    [
        .. recipes.SelectMany(recipe => recipe.Inputs.Concat(recipe.Outputs)).Select(ingredient => ingredient.Item),
        .. saved.Buildables.OfType<SavedProvider>().Select(provider => provider.Resource).OfType<string>(),
        .. nodes.Select(node => node.Resource),
    ];

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static SnapshotDocument FromJson(string json)
    {
        SnapshotDocument document = JsonSerializer.Deserialize<SnapshotDocument>(json, Json)
            ?? throw new JsonException("A snapshot document can't be empty");

        return document.Version switch
        {
            CurrentVersion => document,
            // Imported before stock, or splitter rules and ports, were read: everything else is the same.
            2 or 3 => document with { Version = CurrentVersion },
            // Imported before rates were copied: it traces as before, but the flow analysis knows no rates.
            1 => document with { Version = CurrentVersion, Extractors = [], TransportTiers = [], ResourceNodes = [], Fluids = [] },
            _ => throw new NotSupportedException($"Snapshot document version {document.Version} is not supported"),
        };
    }

    public SnapshotCounts Counts() => new(
        Buildables.Count(buildable => buildable.Role == BuildableDocumentRole.Machine),
        Buildables.Count(buildable => buildable.Role == BuildableDocumentRole.Provider),
        Buildables.Count(buildable => buildable.Role == BuildableDocumentRole.Consumer),
        Buildables.Count(buildable => buildable.Role == BuildableDocumentRole.Logistics),
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
            ResourceNodes.ToDictionary(node => node.Name, StringComparer.Ordinal));

        return new WorldGraph(
            Buildables.Select(buildable => buildable.ToBuildable(copied)),
            Connections.Select(connection => new Connection(BuildableId.Of(connection.From), BuildableId.Of(connection.To), connection.FromPort, connection.ToPort)));
    }
}

/// <summary>The Catalog copy of a snapshot, by id, to look up while building the world.</summary>
internal sealed record CopiedCatalog(
    IReadOnlyDictionary<string, RecipeDocument> Recipes,
    IReadOnlyDictionary<string, ExtractorDocument> Extractors,
    IReadOnlyDictionary<string, TransportTierDocument> TransportTiers,
    IReadOnlyDictionary<string, ResourceNodeDocument> ResourceNodes);

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
    IReadOnlyList<int>? InputPriorities = null)
{
    public static BuildableDocument From(SavedBuildable saved) => FromKind(saved) with
    {
        Position = saved.Position is { } position ? new PositionDocument(position.X, position.Y, position.Z) : null,
        Stock = saved.Stock,
        Burns = saved.Burns?.Order(StringComparer.Ordinal).ToList(),
    };

    private static BuildableDocument FromKind(SavedBuildable saved) => saved switch
    {
        SavedMachine machine => new(machine.Id.Id, BuildableDocumentRole.Machine, machine.ClassName,
            Recipe: machine.Recipe, ClockPercent: Percent(machine.ClockSpeed), ProductionBoost: machine.ProductionBoost),
        SavedProvider provider => new(provider.Id.Id, BuildableDocumentRole.Provider, provider.ClassName,
            ClockPercent: Percent(provider.ClockSpeed), Node: provider.Node, Resource: provider.Resource),
        SavedConsumer consumer => new(consumer.Id.Id, BuildableDocumentRole.Consumer, consumer.ClassName),
        SavedLogistics logistics => new(logistics.Id.Id, BuildableDocumentRole.Logistics, logistics.ClassName, Kind: logistics.Kind,
            SortRules: logistics.SortRules?.Select(rule => new SortRuleDocument(rule.Output, rule.Kind, rule.Item?.Id)).ToList(),
            InputPriorities: logistics.InputPriorities),
        _ => throw new ArgumentException($"{saved} is not a kind of buildable a snapshot knows", nameof(saved)),
    };

    public Buildable ToBuildable(CopiedCatalog catalog)
    {
        var id = BuildableId.Of(Id);
        return Role switch
        {
            // BR-W10: a machine whose recipe is unknown stays in the world, without known inputs or outputs.
            BuildableDocumentRole.Machine => new Machine(
                id,
                Recipe is not null && catalog.Recipes.TryGetValue(Recipe, out RecipeDocument? recipe) ? recipe.ToRecipe() : null,
                Clock(),
                ProductionBoost ?? 1),
            BuildableDocumentRole.Provider => new Provider(id, OfferOf(catalog), StockOf(), BurnsOf()),
            BuildableDocumentRole.Consumer => new Consumer(id, BurnsOf()),
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

    private HashSet<ItemId>? BurnsOf() => Burns?.Select(ItemId.Of).ToHashSet();

    private Dictionary<ItemId, decimal>? StockOf() => Stock?.ToDictionary(entry => ItemId.Of(entry.Key), entry => entry.Value);

    private ClockSpeed Clock() => ClockSpeed.Percent(ClockPercent ?? 100);

    /// <summary>A clock speed is a fraction of 1 (333333/1000000); stored as the percent the game shows.</summary>
    private static decimal Percent(ClockSpeed clockSpeed) => (decimal)clockSpeed.Numerator * 100 / (decimal)clockSpeed.Denominator;
}

/// <summary>Where a buildable stands, in the game's units (centimetres).</summary>
internal sealed record PositionDocument(double X, double Y, double Z);

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
