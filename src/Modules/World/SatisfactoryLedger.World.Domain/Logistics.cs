using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain;

/// <summary>Moves items between buildables without changing them (BR-W8).</summary>
/// <param name="capacity">
/// What a belt, lift or pipe carries at most per minute (BR-A3). Null when there is no limit (splitters, containers,
/// pumps) or the Catalog doesn't know its tier.
/// </param>
public sealed class Logistics(BuildableId id, LogisticsKind kind, Rate? capacity = null) : Buildable(id)
{
    public LogisticsKind Kind { get; } = kind;

    public Rate? Capacity { get; } = capacity;

    /// <summary>A belt or a lift: what a factory's output can leave on (BR-W2).</summary>
    public bool IsBelt => Kind is LogisticsKind.Belt or LogisticsKind.Lift;
}

public enum LogisticsKind
{
    Belt,
    Lift,
    Splitter,
    Merger,
    /// <summary>A storage container: holds solids.</summary>
    StorageContainer,
    /// <summary>A fluid buffer or industrial fluid buffer: holds fluids.</summary>
    FluidBuffer,
    /// <summary>A pipeline segment, pump or valve.</summary>
    Pipe,
    /// <summary>A pipeline junction: splits and merges fluid.</summary>
    PipeJunction,
}
