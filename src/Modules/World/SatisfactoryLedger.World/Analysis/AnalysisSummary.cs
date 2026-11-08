namespace SatisfactoryLedger.World.Analysis;

/// <summary>How the machines of a snapshot run: at 100%, or below it and why (BR-A11).</summary>
internal sealed record AnalysisSummary(int Machines, int Running, int Starved, int OutputBlocked, int NoRecipe);