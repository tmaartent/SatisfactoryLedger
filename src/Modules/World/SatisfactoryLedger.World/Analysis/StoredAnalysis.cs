using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SatisfactoryLedger.World.Persistence;

using SatisfactoryLedger.World.Simulation;
namespace SatisfactoryLedger.World.Analysis;

internal enum AnalysisStatus
{
    /// <summary>Waiting for the background worker, or computed with an older analysis version.</summary>
    Pending,
    Ready,
    Failed,
}

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

internal sealed class StoredAnalysisConfiguration : IEntityTypeConfiguration<StoredAnalysis>
{
    public void Configure(EntityTypeBuilder<StoredAnalysis> analysis)
    {
        analysis.ToTable("snapshot_analyses");
        analysis.HasKey(a => a.SnapshotId);
        // One analysis per snapshot; it goes when its snapshot goes.
        analysis.HasOne<StoredSnapshot>().WithOne().HasForeignKey<StoredAnalysis>(a => a.SnapshotId).OnDelete(DeleteBehavior.Cascade);
        analysis.Property(a => a.Status).HasConversion<string>();
        analysis.HasIndex(a => a.Status);
        analysis.Property(a => a.Version);
        analysis.Property(a => a.ComputedAt);
        analysis.Property(a => a.Duration);
        analysis.Property(a => a.Error);
        analysis.Property(a => a.Content).HasColumnType("jsonb");
        analysis.Ignore(a => a.IsCurrent);
    }
}
