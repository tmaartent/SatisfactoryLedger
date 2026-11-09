using SatisfactoryLedger.World.Contracts.Analysis;
namespace SatisfactoryLedger.World.Analysis;

/// <summary>A snapshot's analysis, as stored (ADR-0017): one row per snapshot, with the result as one jsonb document.</summary>
internal sealed class StoredAnalysis
{
    public required Guid SnapshotId { get; init; }

    public AnalysisStatus Status { get; set; }

    /// <summary>The <see cref="AnalysisDocument.Version"/> it was computed with; 0 while pending.</summary>
    public int Version { get; set; }

    public DateTimeOffset? ComputedAt { get; set; }

    /// <summary>How long the simulation took.</summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>Why it failed, when it did.</summary>
    public string? Error { get; set; }

    /// <summary>The <see cref="AnalysisDocument"/> as JSON, when ready.</summary>
    public string? Content { get; set; }

    /// <summary>Ready, and computed with today's analysis rules.</summary>
    public bool IsCurrent => Status == AnalysisStatus.Ready && Version == AnalysisDocument.CurrentVersion;

    public static StoredAnalysis PendingFor(Guid snapshotId) => new() { SnapshotId = snapshotId, Status = AnalysisStatus.Pending };
}