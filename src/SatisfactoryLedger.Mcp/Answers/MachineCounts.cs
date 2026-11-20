using SatisfactoryLedger.Mcp.LedgerApi;

namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>How many machines there are, and how many run at full speed or are held back, and why.</summary>
internal sealed record MachineCounts(int Total, int Running, int Starved, int OutputBlocked, int NoRecipe)
{
    public static MachineCounts Of(AnalysisSummary summary) =>
        new(summary.Machines, summary.Running, summary.Starved, summary.OutputBlocked, summary.NoRecipe);
}
