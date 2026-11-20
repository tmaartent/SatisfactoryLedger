namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>How many machines run, and how many are held back and why (BR-A11).</summary>
internal sealed record AnalysisSummary(int Machines, int Running, int Starved, int OutputBlocked, int NoRecipe);
