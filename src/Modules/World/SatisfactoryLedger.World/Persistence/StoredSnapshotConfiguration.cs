using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SatisfactoryLedger.World.Persistence;

internal sealed class StoredSnapshotConfiguration : IEntityTypeConfiguration<StoredSnapshot>
{
    public void Configure(EntityTypeBuilder<StoredSnapshot> snapshot)
    {
        snapshot.ToTable("snapshots");
        snapshot.HasKey(s => s.Id);
        snapshot.Property(s => s.SessionName);
        snapshot.Property(s => s.GameBuild);
        snapshot.Property(s => s.SavedAt);
        snapshot.Property(s => s.ImportedAt);
        snapshot.HasIndex(s => s.ImportedAt);

        // Value objects without identity: their fields become columns (counts_machines, ...).
        snapshot.ComplexProperty(s => s.Counts);
        // A list of strings becomes a PostgreSQL text[] column.
        snapshot.PrimitiveCollection(s => s.UnknownRecipes);
        snapshot.ComplexCollection(s => s.Unsupported, unsupported => unsupported.ToJson("unsupported"));

        snapshot.Property(s => s.Content).HasColumnType("jsonb");
    }
}