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

    /// <summary>Which way it faces, seen from above: degrees from the game's x axis towards its y axis (−180 to 180).</summary>
    public double Yaw { get; init; }

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

    /// <summary>
    /// For a drone port that is home to a drone: its route to the port it's paired with (BR-A16). Null when it has no
    /// drone or no paired port.
    /// </summary>
    public SavedDroneRoute? Drone { get; init; }
}

public sealed record SavedPosition(double X, double Y, double Z);

/// <summary>A drone's route (BR-A16): the port it's paired with, the flat distance to it, and how long its latest round trip took.</summary>
/// <param name="LatestTripSeconds">Null when its drone hasn't made a trip yet.</param>
public sealed record SavedDroneRoute(BuildableId Paired, decimal DistanceMetres, decimal? LatestTripSeconds);

/// <param name="Recipe">The recipe's class name (<c>Recipe_IronPlate_C</c>), or null when none is set.</param>
/// <param name="ProductionBoost">The output multiplier from somersloops: 1 without, 2 when all slots are filled.</param>
public sealed record SavedMachine(BuildableId Id, string ClassName, string? Recipe, ClockSpeed ClockSpeed, decimal ProductionBoost)
    : SavedBuildable(Id, ClassName)
{
    /// <summary>The ingredients in it now, per item class name: a moment, like stock, but never supply. Fluids in litres.</summary>
    public IReadOnlyDictionary<string, decimal>? InputContents { get; init; }

    /// <summary>The products waiting in it to leave, per item class name. Fluids in litres.</summary>
    public IReadOnlyDictionary<string, decimal>? OutputContents { get; init; }
}

/// <param name="Node">What it extracts from: a resource node (<c>BP_ResourceNode35</c>) or a water volume.</param>
/// <param name="Resource">
/// The item it extracts, when its class decides that: a water extractor pumps water (<c>Desc_Water_C</c>) wherever it
/// stands. Null when the node decides, as for miners; the Catalog knows the nodes.
/// </param>
public sealed record SavedProvider(BuildableId Id, string ClassName, string? Node, ClockSpeed ClockSpeed, string? Resource = null)
    : SavedBuildable(Id, ClassName);

public sealed record SavedConsumer(BuildableId Id, string ClassName) : SavedBuildable(Id, ClassName)
{
    /// <summary>For a generator: the fuel in it at save time (<c>mCurrentFuelClass</c>); null when its slot is empty, or for other consumers.</summary>
    public string? Fuel { get; init; }

    /// <summary>For a generator: its clock speed (100% when the save says nothing); null for other consumers.</summary>
    public ClockSpeed? ClockSpeed { get; init; }

    /// <summary>True for a dimensional depot uploader: it takes only what's left over (BR-A8).</summary>
    public bool TakesLeftoversOnly { get; init; }

    /// <summary>For a generator: the fuel in it now (<c>mFuelInventory</c>), per item class name. Null for other consumers.</summary>
    public IReadOnlyDictionary<string, decimal>? FuelContents { get; init; }
}

/// <param name="SortRules">A smart or programmable splitter's rules; null for every other piece of logistics.</param>
/// <param name="InputPriorities">
/// A priority merger's priority per input, counted from 0 (<c>Input1</c> is 0). A higher number goes first; 0 is the
/// default. Null for every other piece of logistics.
/// </param>
public sealed record SavedLogistics(
    BuildableId Id,
    string ClassName,
    LogisticsKind Kind,
    IReadOnlyList<SortRule>? SortRules = null,
    IReadOnlyList<int>? InputPriorities = null) : SavedBuildable(Id, ClassName)
{
    /// <summary>How a belt or pipe runs, as built: its spline, in world coordinates. Null for other logistics.</summary>
    public IReadOnlyList<SavedCurvePoint>? Curve { get; init; }

    /// <summary>
    /// What's on a belt or lift, or in a splitter's or merger's buffer, now: per item class name. Unlike stock it's
    /// never counted as supply. Null for logistics that carry no items this way (pipes, containers).
    /// </summary>
    public IReadOnlyDictionary<string, decimal>? Contents { get; init; }
}

/// <summary>
/// A point of a belt's or pipe's curve (the game's <c>mSplineData</c>). Between two points the curve is a cubic Hermite
/// spline: it leaves one point along its <see cref="LeaveTangent"/> and arrives at the next along its
/// <see cref="ArriveTangent"/>. Locations in world coordinates (centimetres); tangents turned with the belt.
/// </summary>
public sealed record SavedCurvePoint(SavedPosition Location, SavedPosition ArriveTangent, SavedPosition LeaveTangent);

/// <summary>Buildables in the item network the adapter can't model yet, such as stations. Their connections are left out.</summary>
public sealed record UnsupportedBuildables(string ClassName, int Count);
