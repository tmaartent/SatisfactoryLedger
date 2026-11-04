namespace SatisfactoryLedger.World.Domain;

/// <summary>Moves items between buildables without changing them (BR-W8).</summary>
public sealed class Logistics(BuildableId id, LogisticsKind kind) : Buildable(id)
{
    public LogisticsKind Kind { get; } = kind;

    /// <summary>A belt or a lift: what a factory's output can leave on (BR-W2).</summary>
    public bool IsBelt => Kind is LogisticsKind.Belt or LogisticsKind.Lift;
}

public enum LogisticsKind
{
    Belt,
    Lift,
    Splitter,
    Merger,
    /// <summary>A storage container, or a fluid buffer.</summary>
    StorageContainer,
    /// <summary>A pipeline segment, pump or valve.</summary>
    Pipe,
    /// <summary>A pipeline junction: splits and merges fluid.</summary>
    PipeJunction,
}
