using System.Text.Json;
using System.Text.Json.Serialization;

using SatisfactoryLedger.Catalog.Contracts;
using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.SaveFile;

namespace SatisfactoryLedger.World.Snapshots;

internal sealed record SnapshotCounts(int Machines, int Providers, int Consumers, int Logistics, int Connections);

/// <summary>
/// The stored content of a world snapshot (ADR-0016): everything the save said, plus the recipes it uses as the
/// Catalog had them at import. A plain document of our own, so the domain model stays free of persistence.
/// </summary>
internal sealed record SnapshotDocument(
    int Version,
    IReadOnlyList<BuildableDocument> Buildables,
    IReadOnlyList<ConnectionDocument> Connections,
    IReadOnlyList<RecipeDocument> Recipes,
    IReadOnlyList<string> UnknownRecipes,
    IReadOnlyList<UnsupportedDocument> Unsupported)
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>A snapshot of a save. Recipes are copied from <paramref name="catalog"/>; the ones it doesn't know are listed (BR-W10).</summary>
    public static SnapshotDocument Create(SavedWorld saved, IReadOnlyCollection<CatalogRecipe> catalog)
    {
        HashSet<string> used = [.. saved.Buildables.OfType<SavedMachine>().Select(machine => machine.Recipe).OfType<string>()];
        List<RecipeDocument> known = [.. catalog.Where(recipe => used.Contains(recipe.Id)).Select(RecipeDocument.From)];
        HashSet<string> knownIds = [.. known.Select(recipe => recipe.Id)];

        return new SnapshotDocument(
            CurrentVersion,
            [.. saved.Buildables.Select(BuildableDocument.From)],
            [.. saved.Connections.Select(connection => new ConnectionDocument(connection.From.Id, connection.To.Id))],
            known,
            [.. used.Where(id => !knownIds.Contains(id)).Order(StringComparer.Ordinal)],
            [.. saved.Unsupported.Select(unsupported => new UnsupportedDocument(unsupported.ClassName, unsupported.Count))]);
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static SnapshotDocument FromJson(string json)
    {
        SnapshotDocument document = JsonSerializer.Deserialize<SnapshotDocument>(json, Json)
            ?? throw new JsonException("A snapshot document can't be empty");
        return document.Version == CurrentVersion
            ? document
            : throw new NotSupportedException($"Snapshot document version {document.Version} is not supported");
    }

    public SnapshotCounts Counts() => new(
        Buildables.Count(buildable => buildable.Role == BuildableDocumentRole.Machine),
        Buildables.Count(buildable => buildable.Role == BuildableDocumentRole.Provider),
        Buildables.Count(buildable => buildable.Role == BuildableDocumentRole.Consumer),
        Buildables.Count(buildable => buildable.Role == BuildableDocumentRole.Logistics),
        Connections.Count);

    /// <summary>The world as a graph the domain can trace and analyse.</summary>
    public WorldGraph ToWorldGraph()
    {
        Dictionary<string, RecipeDocument> recipes = Recipes.ToDictionary(recipe => recipe.Id, StringComparer.Ordinal);

        return new WorldGraph(
            Buildables.Select(buildable => buildable.ToBuildable(recipes)),
            Connections.Select(connection => new Connection(BuildableId.Of(connection.From), BuildableId.Of(connection.To))));
    }
}

internal enum BuildableDocumentRole
{
    Machine,
    Provider,
    Consumer,
    Logistics,
}

/// <param name="ClockPercent">The clock speed as the game shows it (33.3333), exact to its 4 decimals.</param>
internal sealed record BuildableDocument(
    string Id,
    BuildableDocumentRole Role,
    string ClassName,
    string? Recipe = null,
    decimal? ClockPercent = null,
    decimal? ProductionBoost = null,
    string? Node = null,
    LogisticsKind? Kind = null)
{
    public static BuildableDocument From(SavedBuildable saved) => saved switch
    {
        SavedMachine machine => new(machine.Id.Id, BuildableDocumentRole.Machine, machine.ClassName,
            Recipe: machine.Recipe, ClockPercent: Percent(machine.ClockSpeed), ProductionBoost: machine.ProductionBoost),
        SavedProvider provider => new(provider.Id.Id, BuildableDocumentRole.Provider, provider.ClassName,
            ClockPercent: Percent(provider.ClockSpeed), Node: provider.Node),
        SavedConsumer consumer => new(consumer.Id.Id, BuildableDocumentRole.Consumer, consumer.ClassName),
        SavedLogistics logistics => new(logistics.Id.Id, BuildableDocumentRole.Logistics, logistics.ClassName, Kind: logistics.Kind),
        _ => throw new ArgumentException($"{saved} is not a kind of buildable a snapshot knows", nameof(saved)),
    };

    public Buildable ToBuildable(IReadOnlyDictionary<string, RecipeDocument> recipes)
    {
        var id = BuildableId.Of(Id);
        return Role switch
        {
            // BR-W10: a machine whose recipe is unknown stays in the world, without known inputs or outputs.
            BuildableDocumentRole.Machine when Recipe is not null && recipes.TryGetValue(Recipe, out RecipeDocument? recipe) =>
                new Machine(id, recipe.Inputs.Select(input => ItemId.Of(input.Item)), recipe.Outputs.Select(output => ItemId.Of(output.Item))),
            BuildableDocumentRole.Machine => new Machine(id, [], []),
            BuildableDocumentRole.Provider => new Provider(id),
            BuildableDocumentRole.Consumer => new Consumer(id),
            BuildableDocumentRole.Logistics => new Logistics(id, Kind ?? throw new JsonException($"Logistics {Id} has no kind")),
            _ => throw new JsonException($"Buildable {Id} has an unknown role"),
        };
    }

    /// <summary>A clock speed is a fraction of 1 (333333/1000000); stored as the percent the game shows.</summary>
    private static decimal Percent(ClockSpeed clockSpeed) => (decimal)clockSpeed.Numerator * 100 / (decimal)clockSpeed.Denominator;
}

internal sealed record ConnectionDocument(string From, string To);

internal sealed record RecipeDocument(string Id, decimal CycleSeconds, IReadOnlyList<IngredientDocument> Inputs, IReadOnlyList<IngredientDocument> Outputs)
{
    public static RecipeDocument From(CatalogRecipe recipe) => new(
        recipe.Id,
        recipe.CycleSeconds,
        [.. recipe.Inputs.Select(input => new IngredientDocument(input.ItemId, input.AmountPerCycle))],
        [.. recipe.Outputs.Select(output => new IngredientDocument(output.ItemId, output.AmountPerCycle))]);
}

internal sealed record IngredientDocument(string Item, decimal AmountPerCycle);

internal sealed record UnsupportedDocument(string ClassName, int Count);
