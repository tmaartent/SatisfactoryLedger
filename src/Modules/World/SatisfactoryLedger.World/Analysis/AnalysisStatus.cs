namespace SatisfactoryLedger.World.Analysis;

internal enum AnalysisStatus
{
    /// <summary>Waiting for the background worker, or computed with an older analysis version.</summary>
    Pending,
    Ready,
    Failed,
}