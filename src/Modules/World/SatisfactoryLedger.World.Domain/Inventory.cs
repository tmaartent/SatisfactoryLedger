namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// BR-A7: the inventory of something at the end of a line (a storage container, fluid buffer, a station that loads, the
/// dimensional depot), and whether it still takes an item. It takes everything until it's full, as in the game.
/// Plain data, so the flow network can carry it to the analysis service (ADR-0018).
/// </summary>
/// <param name="Size">Its slots, its volume in m³, or for the depot its stacks per item (its upgrades).</param>
/// <param name="Holds">What it holds now, per item: a count, or m³ of fluid.</param>
/// <param name="StackSizes">How many of each item it holds fit in a slot. An item missing here can't be called full.</param>
public sealed record Inventory(InventoryKind Kind, decimal Size, IReadOnlyDictionary<ItemId, decimal> Holds, IReadOnlyDictionary<ItemId, int> StackSizes)
{
    /// <summary>
    /// The items the save shows still arriving, though it may be full, per minute: what the running machines feeding it
    /// with room in their output make, so something empties it at least that fast (BR-A7). As the flow network counts
    /// rates (ADR-0004, amended).
    /// </summary>
    public IReadOnlyDictionary<ItemId, double> Arriving { get; init; } = new Dictionary<ItemId, double>();

    /// <summary>
    /// An inventory of slots, each a stack of one item. An item fits while a slot is empty, or while a stack of that item
    /// isn't full.
    /// </summary>
    public static Inventory OfSlots(int slots, IReadOnlyDictionary<ItemId, decimal> holds, IReadOnlyDictionary<ItemId, int> stackSizes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(slots);
        return new Inventory(InventoryKind.Slots, slots, holds, stackSizes);
    }

    /// <summary>A fluid buffer: it takes fluid until it holds its volume, in m³.</summary>
    public static Inventory OfFluid(decimal cubicMetres, IReadOnlyDictionary<ItemId, decimal> holds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cubicMetres);
        return new Inventory(InventoryKind.Fluid, cubicMetres, holds, new Dictionary<ItemId, int>());
    }

    /// <summary>
    /// The dimensional depot: an uploader takes an item until the depot holds its limit of it, the item's stack size
    /// times <paramref name="stacks"/> (the depot's upgrades).
    /// </summary>
    public static Inventory OfDepot(IReadOnlyDictionary<ItemId, decimal> holds, IReadOnlyDictionary<ItemId, int> stackSizes, int stacks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stacks);
        return new Inventory(InventoryKind.Depot, stacks, holds, stackSizes);
    }

    /// <summary>
    /// The depot's stacks per item, read from what it holds: the save doesn't store its upgrades, but no item can be
    /// held beyond its limit, so an item at its limit shows them as stack size × stacks. 0 when it holds nothing of a
    /// known stack size.
    /// </summary>
    public static int DepotStacks(IReadOnlyDictionary<ItemId, decimal> holds, IReadOnlyDictionary<ItemId, int> stackSizes) =>
        holds.Where(entry => stackSizes.GetValueOrDefault(entry.Key) > 0)
            .Select(entry => (int)Math.Ceiling(entry.Value / stackSizes[entry.Key]))
            .DefaultIfEmpty(0)
            .Max();

    /// <summary>Whether one more of <paramref name="item"/> fits.</summary>
    public bool Takes(ItemId item) => TakesPerMinute(item) > 0;

    /// <summary>
    /// How much of <paramref name="item"/> it takes a minute: everything while it fits, otherwise what the save shows
    /// arriving (BR-A7), and nothing when nothing does.
    /// </summary>
    public double TakesPerMinute(ItemId item) => Fits(item) ? double.PositiveInfinity : Arriving.GetValueOrDefault(item);

    private bool Fits(ItemId item) => Kind switch
    {
        InventoryKind.Fluid => Holds.Values.Sum() < Size,
        InventoryKind.Depot => StackSizes.GetValueOrDefault(item) is not (> 0 and var stackSize) || Holds.GetValueOrDefault(item) < stackSize * Size,
        _ => SlotsTake(item),
    };

    /// <summary>
    /// An empty slot takes anything; without one, only a stack of the item that isn't full. When the stack size of
    /// anything it holds isn't known (a Catalog imported before stack sizes), it can't be called full.
    /// </summary>
    private bool SlotsTake(ItemId item)
    {
        List<KeyValuePair<ItemId, decimal>> held = [.. Holds.Where(entry => entry.Value > 0)];
        if (held.Any(entry => StackSizes.GetValueOrDefault(entry.Key) <= 0))
        {
            return true;
        }

        decimal used = held.Sum(entry => Math.Ceiling(entry.Value / StackSizes[entry.Key]));
        return used < Size || (Holds.TryGetValue(item, out decimal count) && count % StackSizes[item] != 0);
    }
}
