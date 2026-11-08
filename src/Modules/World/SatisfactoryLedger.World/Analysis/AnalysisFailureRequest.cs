namespace SatisfactoryLedger.World.Analysis;

/// <param name="Error">Why the analysis service couldn't analyse the snapshot.</param>
internal sealed record AnalysisFailureRequest(string? Error);