using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.SaveFile;

/// <summary>
/// What a save file says about the world, in our own terms (ADR-0013). Recipes, extractors and nodes are still
/// class names here: turning them into items and rates takes the Catalog, which the adapter doesn't know.
/// </summary>
public sealed record SavedWorld(
    SaveInfo Info,
    IReadOnlyList<SavedBuildable> Buildables,
    IReadOnlyList<Connection> Connections,
    IReadOnlyList<UnsupportedBuildables> Unsupported);

/// <param name="GameBuild">The game's build number, e.g. 517554. Saves and Docs files of the same build belong together.</param>
public sealed record SaveInfo(string SessionName, int GameBuild, DateTime SavedAtUtc);

/// <summary>A buildable in the item network. <see cref="ClassName"/> is the game's class, e.g. <c>Build_ConveyorBeltMk6_C</c>.</summary>
public abstract record SavedBuildable(BuildableId Id, string ClassName);

/// <param name="Recipe">The recipe's class name (<c>Recipe_IronPlate_C</c>), or null when none is set.</param>
/// <param name="ProductionBoost">The output multiplier from somersloops: 1 without, 2 when all slots are filled.</param>
public sealed record SavedMachine(BuildableId Id, string ClassName, string? Recipe, ClockSpeed ClockSpeed, decimal ProductionBoost)
    : SavedBuildable(Id, ClassName);

/// <param name="Node">What it extracts from: a resource node (<c>BP_ResourceNode35</c>) or a water volume.</param>
public sealed record SavedProvider(BuildableId Id, string ClassName, string? Node, ClockSpeed ClockSpeed)
    : SavedBuildable(Id, ClassName);

public sealed record SavedConsumer(BuildableId Id, string ClassName) : SavedBuildable(Id, ClassName);

public sealed record SavedLogistics(BuildableId Id, string ClassName, LogisticsKind Kind) : SavedBuildable(Id, ClassName);

/// <summary>Buildables in the item network the adapter can't model yet, such as stations. Their connections are left out.</summary>
public sealed record UnsupportedBuildables(string ClassName, int Count);
