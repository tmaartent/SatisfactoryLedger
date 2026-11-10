using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain;

/// <summary>What a recipe takes and makes per minute at 100% clock speed, as the Catalog had it when the save was imported.</summary>
public sealed record Recipe(IReadOnlyDictionary<ItemId, Rate> Inputs, IReadOnlyDictionary<ItemId, Rate> Outputs)
{
    /// <summary>What one cycle makes of each output; empty when that isn't known (BR-A7).</summary>
    public IReadOnlyDictionary<ItemId, decimal> Batches { get; init; } = new Dictionary<ItemId, decimal>();
}
