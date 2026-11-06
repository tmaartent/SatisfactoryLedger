using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.World.Domain;

/// <summary>Moves items between buildables without changing them (BR-W8).</summary>
/// <param name="capacity">
/// What a belt, lift or pipe carries at most per minute (BR-A3). Null when there is no limit (splitters, containers,
/// pumps) or the Catalog doesn't know its tier.
/// </param>
/// <param name="stock">What a storage container holds now, per item.</param>
/// <param name="sortRules">A smart or programmable splitter's rules per output (BR-A4).</param>
/// <param name="inputPriorities">A priority merger's priority per input: a higher number goes first (BR-A5).</param>
public sealed class Logistics(
    BuildableId id,
    LogisticsKind kind,
    Rate? capacity = null,
    IReadOnlyDictionary<ItemId, decimal>? stock = null,
    IReadOnlyList<SortRule>? sortRules = null,
    IReadOnlyList<int>? inputPriorities = null) : Buildable(id)
{
    /// <summary>A priority merger's priority per input, counted from 0. Empty for a plain merger and everything else.</summary>
    public IReadOnlyList<int> InputPriorities { get; } = inputPriorities ?? [];

    /// <summary>A smart or programmable splitter's rules per output. Empty for a plain splitter and everything else.</summary>
    public IReadOnlyList<SortRule> SortRules { get; } = sortRules ?? [];

    public LogisticsKind Kind { get; } = kind;

    public Rate? Capacity { get; } = capacity;

    /// <summary>What a storage container holds now. Empty for everything else, and when it isn't known.</summary>
    public IReadOnlyDictionary<ItemId, decimal> Stock { get; } = stock ?? new Dictionary<ItemId, decimal>();

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
