using SatisfactoryLedger.SharedKernel;
using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.SaveFile;

public sealed record SavedConsumer(BuildableId Id, string ClassName) : SavedBuildable(Id, ClassName)
{
    /// <summary>For a generator: the fuel in it at save time (<c>mCurrentFuelClass</c>); null when its slot is empty, or for other consumers.</summary>
    public string? Fuel { get; init; }

    /// <summary>For a generator: its clock speed (100% when the save says nothing); null for other consumers.</summary>
    public ClockSpeed? ClockSpeed { get; init; }

    /// <summary>True for a dimensional depot uploader: it takes only what's left over (BR-A8).</summary>
    public bool TakesLeftoversOnly { get; init; }

    /// <summary>For a generator: the fuel in it now (<c>mFuelInventory</c>), per item class name. Null for other consumers.</summary>
    public IReadOnlyDictionary<string, decimal>? FuelContents { get; init; }
}