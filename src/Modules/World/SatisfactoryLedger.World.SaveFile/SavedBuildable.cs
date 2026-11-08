using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.SaveFile;

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