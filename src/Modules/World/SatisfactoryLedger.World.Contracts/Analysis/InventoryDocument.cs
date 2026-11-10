namespace SatisfactoryLedger.World.Contracts.Analysis;

/// <summary>An inventory at the end of a line (BR-A7): it takes an item only while that fits.</summary>
/// <param name="Size">Its slots, its volume in m³, or for the depot its stacks per item.</param>
/// <param name="Holds">What it holds now, per item class name.</param>
/// <param name="StackSizes">Per item class name it holds, how many fit in a slot.</param>
/// <param name="Arriving">Per item class name, what the save shows still arriving per minute, though it may be full; it takes that much.</param>
public sealed record InventoryDocument(
    InventoryKind Kind,
    decimal Size,
    IReadOnlyDictionary<string, decimal> Holds,
    IReadOnlyDictionary<string, int> StackSizes,
    IReadOnlyDictionary<string, double>? Arriving = null);
