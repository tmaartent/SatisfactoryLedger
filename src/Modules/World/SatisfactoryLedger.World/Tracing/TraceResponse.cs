namespace SatisfactoryLedger.World.Tracing;

/// <summary>
/// A trace of the machines of one class and recipe: how many there are, how many were traced (the starved ones, or the
/// output-blocked ones), and where their shortage or blockage starts.
/// </summary>
internal sealed record TraceResponse(string Building, string? Recipe, int Machines, int Traced, IReadOnlyList<TraceStartResponse> Starts);
