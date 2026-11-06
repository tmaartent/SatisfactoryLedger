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
public abstract record SavedBuildable(BuildableId Id, string ClassName)
{
    /// <summary>Where it stands, in the game's units (centimetres). Lets a player find it in the game.</summary>
    public SavedPosition? Position { get; init; }

    /// <summary>
    /// What it holds now, per item class name: what's on a platform, what a drone port received, what's in a storage
    /// container. Null when the save says nothing about it.
    /// </summary>
    public IReadOnlyDictionary<string, decimal>? Stock { get; init; }

    /// <summary>
    /// The fuel a drone port or truck station burns, as item class names: what's in its fuel slot now. Empty when the
    /// slot is empty, so the save can't tell; null for buildables without a fuel slot.
    /// </summary>
    public IReadOnlySet<string>? Burns { get; init; }
}

public sealed record SavedPosition(double X, double Y, double Z);

/// <param name="Recipe">The recipe's class name (<c>Recipe_IronPlate_C</c>), or null when none is set.</param>
/// <param name="ProductionBoost">The output multiplier from somersloops: 1 without, 2 when all slots are filled.</param>
public sealed record SavedMachine(BuildableId Id, string ClassName, string? Recipe, ClockSpeed ClockSpeed, decimal ProductionBoost)
    : SavedBuildable(Id, ClassName);

/// <param name="Node">What it extracts from: a resource node (<c>BP_ResourceNode35</c>) or a water volume.</param>
/// <param name="Resource">
/// The item it extracts, when its class decides that: a water extractor pumps water (<c>Desc_Water_C</c>) wherever it
/// stands. Null when the node decides, as for miners; the Catalog knows the nodes.
/// </param>
public sealed record SavedProvider(BuildableId Id, string ClassName, string? Node, ClockSpeed ClockSpeed, string? Resource = null)
    : SavedBuildable(Id, ClassName);

public sealed record SavedConsumer(BuildableId Id, string ClassName) : SavedBuildable(Id, ClassName);

public sealed record SavedLogistics(BuildableId Id, string ClassName, LogisticsKind Kind) : SavedBuildable(Id, ClassName);

/// <summary>Buildables in the item network the adapter can't model yet, such as stations. Their connections are left out.</summary>
public sealed record UnsupportedBuildables(string ClassName, int Count);
