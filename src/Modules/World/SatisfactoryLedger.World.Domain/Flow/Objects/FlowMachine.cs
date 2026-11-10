namespace SatisfactoryLedger.World.Domain.Flow.Objects;

/// <param name="Demand">Per input item, what it asks per minute at 100% utilisation: recipe rate × clock speed.</param>
/// <param name="Production">Per output item, what it makes per minute at 100%: recipe rate × clock speed × production boost.</param>
/// <param name="Stocked">
/// The inputs whose stock inside it lasts at least <see cref="SteadyState.ConfirmedMinutes"/> at its demand: they count
/// as supplied, whatever comes in (BR-A17). It still takes what its belts bring of them.
/// </param>
/// <param name="Leaving">
/// The products the save shows leaving it, with room for its next batch: they never hold it back (BR-A7).
/// </param>
public sealed record FlowMachine(
    BuildableId Id,
    IReadOnlyDictionary<ItemId, double> Demand,
    IReadOnlyDictionary<ItemId, double> Production,
    IReadOnlySet<ItemId>? Stocked = null,
    IReadOnlySet<ItemId>? Leaving = null) : FlowNode(Id);