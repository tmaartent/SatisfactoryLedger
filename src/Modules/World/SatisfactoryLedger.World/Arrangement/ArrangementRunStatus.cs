namespace SatisfactoryLedger.World.Arrangement;

/// <summary>Where a run of the optimal arrangement stands.</summary>
internal enum ArrangementRunStatus
{
    /// <summary>Requested; the analysis service hasn't sent it back yet.</summary>
    Pending,

    Ready,

    Failed,
}
