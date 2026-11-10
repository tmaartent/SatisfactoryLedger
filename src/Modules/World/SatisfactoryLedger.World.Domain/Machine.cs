using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain;

/// <summary>A building that runs a recipe: smelter, constructor, assembler, refinery, …</summary>
public sealed class Machine : Buildable
{
    /// <param name="recipe">Null when its recipe isn't known (BR-W10): it then asks and makes nothing.</param>
    /// <param name="productionBoost">The output multiplier from somersloops: 1 without, 2 when all slots are filled.</param>
    public Machine(BuildableId id, Recipe? recipe, ClockSpeed clockSpeed, decimal productionBoost = 1) : base(id)
    {
        ArgumentNullException.ThrowIfNull(clockSpeed);
        ArgumentOutOfRangeException.ThrowIfLessThan(productionBoost, 1);

        // BR-A1: the clock speed scales both sides; somersloops only raise the output (BR-B11).
        Demand = recipe?.Inputs.ToDictionary(input => input.Key, input => input.Value * clockSpeed) ?? [];
        Production = recipe?.Outputs.ToDictionary(output => output.Key, output => output.Value * clockSpeed * productionBoost) ?? [];
        Batches = recipe?.Batches.ToDictionary(batch => batch.Key, batch => batch.Value * productionBoost) ?? [];
        Consumes = Demand.Keys.ToHashSet();
        Produces = Production.Keys.ToHashSet();
    }

    /// <summary>
    /// What it holds of its inputs, per item (fluids in m³), at save time. A stock that lasts counts as supplied (BR-A17).
    /// </summary>
    public IReadOnlyDictionary<ItemId, decimal> InputStock { get; init; } = new Dictionary<ItemId, decimal>();

    /// <summary>
    /// What waits to leave it, per item (fluids in m³), at save time; null when the save says nothing about it (BR-A7).
    /// </summary>
    public IReadOnlyDictionary<ItemId, decimal>? OutputStock { get; init; }

    /// <summary>Per input item, what it asks per minute when it runs at 100%.</summary>
    public IReadOnlyDictionary<ItemId, Rate> Demand { get; }

    /// <summary>Per output item, what it makes per minute when it runs at 100%.</summary>
    public IReadOnlyDictionary<ItemId, Rate> Production { get; }

    /// <summary>How many of each of its products fit in a slot, from the Catalog; empty when that isn't known (BR-A7).</summary>
    public IReadOnlyDictionary<ItemId, int> StackSizes { get; init; } = new Dictionary<ItemId, int>();

    /// <summary>
    /// BR-A7: the products the save shows leaving it: it can run and has room for its next batch. Their line isn't
    /// full, whatever takes them further on, so they never hold it back (decided 2026-11-10).
    /// </summary>
    public IReadOnlySet<ItemId> Leaving =>
        Produces.Where(item => StackSizes.GetValueOrDefault(item) is > 0 and var stackSize && ShowsLeaving(item, stackSize)).ToHashSet();

    /// <summary>Per output item, what one cycle makes, somersloops included; empty when that isn't known (BR-A7).</summary>
    public IReadOnlyDictionary<ItemId, decimal> Batches { get; }

    /// <summary>The items its recipe takes in.</summary>
    public IReadOnlySet<ItemId> Consumes { get; }

    /// <summary>The items its recipe makes.</summary>
    public IReadOnlySet<ItemId> Produces { get; }

    /// <summary>
    /// BR-A7: whether the save shows <paramref name="item"/> leaving it: it holds some of every input, so it can run,
    /// and its output slot (a stack) has room for more than its next batch. A backed-up machine fills its slot, waits
    /// until one batch fits again and fills it, so its slot always sits between the stack less a batch and the stack:
    /// that shows it backing up (decided 2026-11-10). An empty slot of a machine that can't run shows nothing. Without a
    /// known batch, any room counts.
    /// </summary>
    public bool ShowsLeaving(ItemId item, int stackSize) =>
        OutputStock is { } waiting
        && Produces.Contains(item)
        && Consumes.All(input => InputStock.GetValueOrDefault(input) > 0)
        && (Batches.TryGetValue(item, out decimal batch) && batch > 0
            ? waiting.GetValueOrDefault(item) + batch < stackSize
            : waiting.GetValueOrDefault(item) < stackSize);

    public bool ProducesAnyOf(IReadOnlySet<ItemId> items) => Produces.Overlaps(items);
}
