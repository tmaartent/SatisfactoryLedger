using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SatisfactoryLedger.World.Persistence;
namespace SatisfactoryLedger.World.Analysis;

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