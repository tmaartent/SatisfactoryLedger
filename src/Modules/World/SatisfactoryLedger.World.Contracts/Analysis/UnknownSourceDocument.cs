namespace SatisfactoryLedger.World.Contracts.Analysis;

/// <param name="BurnRate">For a drone port: its fuel per minute; null when that isn't known (BR-A16).</param>
/// <param name="Cargo">An empty station's cargo: what its neighbours hold (BR-A9); null when not known.</param>
public sealed record UnknownSourceDocument(
    string Id, IReadOnlyDictionary<string, double> Stock, Carries Carries, IReadOnlyList<string>? Burns = null, double? BurnRate = null,
    InventoryDocument? Inventory = null, IReadOnlyList<string>? Cargo = null) : FlowNodeDocument(Id);