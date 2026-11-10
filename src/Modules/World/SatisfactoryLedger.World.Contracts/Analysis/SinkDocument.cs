namespace SatisfactoryLedger.World.Contracts.Analysis;

/// <param name="BurnRate">For a drone port that loads: its fuel per minute; null when that isn't known (BR-A16).</param>
/// <param name="Inventory">At the end of a line, what it holds and how much fits (BR-A7); null when it takes everything.</param>
public sealed record SinkDocument(
    string Id, IReadOnlyList<string>? Burns = null, bool LeftoversOnly = false, double? BurnRate = null, InventoryDocument? Inventory = null) : FlowNodeDocument(Id);