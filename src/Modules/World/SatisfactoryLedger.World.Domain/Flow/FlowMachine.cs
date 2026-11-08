namespace SatisfactoryLedger.World.Domain.Flow;

/// <param name="Demand">Per input item, what it asks per minute at 100% utilisation: recipe rate × clock speed.</param>
/// <param name="Production">Per output item, what it makes per minute at 100%: recipe rate × clock speed × production boost.</param>
/// <param name="Stocked">
/// The inputs whose stock inside it lasts at least <see cref="SteadyState.ConfirmedMinutes"/> at its demand: they count
/// as supplied, whatever comes in (BR-A17). It still takes what its belts bring of them.
/// </param>
public sealed record FlowMachine(
    BuildableId Id,
    IReadOnlyDictionary<ItemId, double> Demand,
    IReadOnlyDictionary<ItemId, double> Production,
    IReadOnlySet<ItemId>? Stocked = null) : FlowNode(Id);