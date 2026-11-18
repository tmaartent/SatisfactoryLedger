namespace SatisfactoryLedger.World.Arrangement;

/// <summary>Why the analysis service couldn't compute an arrangement.</summary>
internal sealed record ArrangementFailureRequest(string? Error);
