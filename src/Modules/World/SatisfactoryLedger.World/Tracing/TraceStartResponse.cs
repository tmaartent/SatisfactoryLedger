using SatisfactoryLedger.World.Domain.Tracing;

namespace SatisfactoryLedger.World.Tracing;

/// <summary>
/// Where a shortage or blockage starts (decided 2026-11-20): why, of which item, at which buildables, the steps that
/// lead there, and what those buildables make, offer, carry or take of it per minute. <see cref="Others"/>: who else
/// takes it (taken by others) or sends it (supplied by others). <see cref="ShortOf"/>: what starved consumers lack.
/// </summary>
internal sealed record TraceStartResponse(
    TraceCause Cause,
    string Item,
    IReadOnlyList<TraceGroupResponse> At,
    IReadOnlyList<TraceStepResponse> Path,
    double PerMinute,
    IReadOnlyList<TraceGroupResponse> Others,
    IReadOnlyDictionary<string, double> ShortOf);
