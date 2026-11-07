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
        Consumes = Demand.Keys.ToHashSet();
        Produces = Production.Keys.ToHashSet();
    }

    /// <summary>
    /// What it holds of its inputs, per item (fluids in m³), at save time. A stock that lasts counts as supplied (BR-A17).
    /// </summary>
    public IReadOnlyDictionary<ItemId, decimal> InputStock { get; init; } = new Dictionary<ItemId, decimal>();

    /// <summary>Per input item, what it asks per minute when it runs at 100%.</summary>
    public IReadOnlyDictionary<ItemId, Rate> Demand { get; }

    /// <summary>Per output item, what it makes per minute when it runs at 100%.</summary>
    public IReadOnlyDictionary<ItemId, Rate> Production { get; }

    /// <summary>The items its recipe takes in.</summary>
    public IReadOnlySet<ItemId> Consumes { get; }

    /// <summary>The items its recipe makes.</summary>
    public IReadOnlySet<ItemId> Produces { get; }

    public bool ProducesAnyOf(IReadOnlySet<ItemId> items) => Produces.Overlaps(items);
}
