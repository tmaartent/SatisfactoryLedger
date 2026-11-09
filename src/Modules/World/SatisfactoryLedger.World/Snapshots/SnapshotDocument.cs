using System.Text.Json;
using System.Text.Json.Serialization;

using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.SaveFile;

namespace SatisfactoryLedger.World.Snapshots;

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
    /// Version 10 added the volumes of footprints, to draw buildings in 3D; a version 9 snapshot has none.
    /// </summary>
    public const int CurrentVersion = 10;

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
                .Select(footprint => new FootprintDocument(footprint.Id, footprint.MinX, footprint.MinY, footprint.MaxX, footprint.MaxY,
                    [.. footprint.Volumes.Select(v => new VolumeDocument(
                        v.MinX, v.MinY, v.MinZ, v.MaxX, v.MaxY, v.MaxZ, v.RotationX, v.RotationY, v.RotationZ, v.RotationW, v.OffsetX, v.OffsetY, v.OffsetZ))]))],
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
            2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 => document with { Version = CurrentVersion },
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