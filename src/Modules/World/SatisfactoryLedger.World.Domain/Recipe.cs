using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain;

/// <summary>What a recipe takes and makes per minute at 100% clock speed, as the Catalog had it when the save was imported.</summary>
public sealed record Recipe(IReadOnlyDictionary<ItemId, Rate> Inputs, IReadOnlyDictionary<ItemId, Rate> Outputs);
