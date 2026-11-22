namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>Where a shortage or blockage starts, as World's trace endpoints give it.</summary>
internal sealed record TraceStartResponse(
    string Cause,
    string Item,
    IReadOnlyList<TraceGroupResponse> At,
    IReadOnlyList<TraceStepResponse> Path,
    double PerMinute,
    IReadOnlyList<TraceGroupResponse> Others,
    IReadOnlyDictionary<string, double> ShortOf,
    MoreCapacityResponse? MoreCapacity);
