using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SatisfactoryLedger.World.Persistence;

namespace SatisfactoryLedger.World.Arrangement;

internal sealed class StoredArrangementConfiguration : IEntityTypeConfiguration<StoredArrangement>
{
    public void Configure(EntityTypeBuilder<StoredArrangement> arrangement)
    {
        arrangement.ToTable("arrangements");
        arrangement.HasKey(a => a.Id);
        // A run is made from one snapshot; it goes when its snapshot goes.
        arrangement.HasOne<StoredSnapshot>().WithMany().HasForeignKey(a => a.SnapshotId).OnDelete(DeleteBehavior.Cascade);
        arrangement.HasIndex(a => a.World);
        arrangement.Property(a => a.Status).HasConversion<string>();
        arrangement.Property(a => a.Problem).HasColumnType("jsonb");
        arrangement.Property(a => a.Proposal).HasColumnType("jsonb");
        // A list of rectangles as JSON; compared by its items, so a change to it is seen.
        arrangement.Property(a => a.Exclusions)
            .HasColumnType("jsonb")
            .HasConversion(
                exclusions => JsonSerializer.Serialize(exclusions, JsonSerializerOptions.Default),
                json => JsonSerializer.Deserialize<List<WorldRectangle>>(json, JsonSerializerOptions.Default) ?? new List<WorldRectangle>(),
                new ValueComparer<List<WorldRectangle>>(
                    (a, b) => a!.SequenceEqual(b!),
                    exclusions => exclusions.Aggregate(0, (hash, exclusion) => HashCode.Combine(hash, exclusion)),
                    exclusions => exclusions.ToList()));
        arrangement.Ignore(a => a.Area);
        arrangement.Ignore(a => a.Settings);
        arrangement.Ignore(a => a.Grid);
    }
}
