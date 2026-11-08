using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.SaveFile;

/// <param name="Recipe">The recipe's class name (<c>Recipe_IronPlate_C</c>), or null when none is set.</param>
/// <param name="ProductionBoost">The output multiplier from somersloops: 1 without, 2 when all slots are filled.</param>
public sealed record SavedMachine(BuildableId Id, string ClassName, string? Recipe, ClockSpeed ClockSpeed, decimal ProductionBoost)
    : SavedBuildable(Id, ClassName)
{
    /// <summary>The ingredients in it now, per item class name: a moment, like stock, but never supply. Fluids in litres.</summary>
    public IReadOnlyDictionary<string, decimal>? InputContents { get; init; }

    /// <summary>The products waiting in it to leave, per item class name. Fluids in litres.</summary>
    public IReadOnlyDictionary<string, decimal>? OutputContents { get; init; }
}