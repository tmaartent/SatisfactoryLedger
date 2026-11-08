using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SatisfactoryLedger.Catalog.Domain;

namespace SatisfactoryLedger.Catalog.Persistence;

internal sealed class FootprintConfiguration : IEntityTypeConfiguration<Footprint>
{
    public void Configure(EntityTypeBuilder<Footprint> footprint)
    {
        footprint.HasKey(f => f.Id);
        footprint.Property(f => f.Id).HasConversion(id => id.Id, value => FootprintId.Of(value));
        footprint.Property(f => f.MinX);
        footprint.Property(f => f.MinY);
        footprint.Property(f => f.MaxX);
        footprint.Property(f => f.MaxY);
        footprint.Property(f => f.IsInGame);
        footprint.Ignore(f => f.Width);
        footprint.Ignore(f => f.Length);
    }
}