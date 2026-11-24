using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.SaveFile;

/// <param name="SortRules">A smart or programmable splitter's rules; null for every other piece of logistics.</param>
/// <param name="InputPriorities">
/// A priority merger's priority per input, counted from 0 (<c>Input1</c> is 0). A higher number goes first; 0 is the
/// default. Null for every other piece of logistics.
/// </param>
public sealed record SavedLogistics(
    BuildableId Id,
    string ClassName,
    LogisticsKind Kind,
    IReadOnlyList<SortRule>? SortRules = null,
    IReadOnlyList<int>? InputPriorities = null) : SavedBuildable(Id, ClassName)
{
    /// <summary>How a belt or pipe runs, as built: its spline, in world coordinates. Null for other logistics.</summary>
    public IReadOnlyList<SavedCurvePoint>? Curve { get; init; }

    /// <summary>
    /// What's on a belt or lift, or in a splitter's or merger's buffer, or the fluid in a pipe, pump or junction in m³,
    /// now: per item class name. Unlike stock it's never counted as supply. Null for logistics that carry nothing this
    /// way (containers, fluid buffers).
    /// </summary>
    public IReadOnlyDictionary<string, decimal>? Contents { get; init; }

    /// <summary>
    /// How many ends of a belt, lift or pipe are connected to nothing (asked 2026-11-23): items can't go on, or come in,
    /// there. An end linked to a building the ledger doesn't read counts as connected. 0 for other logistics.
    /// </summary>
    public int LooseEnds { get; init; }

    /// <summary>For a pump, the pipe piece it pushes fluid into (BR-A22); null for other logistics, and when it's linked to nothing there.</summary>
    public BuildableId? PumpsInto { get; init; }
}