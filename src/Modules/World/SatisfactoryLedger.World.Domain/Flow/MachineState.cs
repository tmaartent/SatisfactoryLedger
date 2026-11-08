namespace SatisfactoryLedger.World.Domain.Flow;

/// <param name="Utilisation">Between 0 and 1 (BR-A1).</param>
/// <param name="LimitingItem">The item it's starved of or blocked on; null when <see cref="Limit"/> is None or NoRecipe.</param>
/// <param name="StarvedOf">
/// Every input it's offered less of than it needs at 100%, with the share it's offered (BR-A11). Empty when none is.
/// </param>
/// <param name="BlockedOn">Every product its outputs can't take all of at 100%, with the share they can take. Empty when none.</param>
public sealed record MachineState(
    BuildableId Machine,
    double Utilisation,
    MachineLimit Limit,
    ItemId? LimitingItem,
    IReadOnlyDictionary<ItemId, double>? StarvedOf = null,
    IReadOnlyDictionary<ItemId, double>? BlockedOn = null);