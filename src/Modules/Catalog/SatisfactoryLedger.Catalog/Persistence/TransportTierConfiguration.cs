using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SatisfactoryLedger.Catalog.Domain;

namespace SatisfactoryLedger.Catalog.Persistence;

internal sealed class TransportTierConfiguration : IEntityTypeConfiguration<TransportTier>
{
    public void Configure(EntityTypeBuilder<TransportTier> tier)
    {
        tier.HasKey(t => t.Id);
        tier.Property(t => t.Id).HasConversion(id => id.Id, value => TransportTierId.Of(value));
        tier.Property(t => t.Name);
        tier.Property(t => t.Form).HasConversion<string>();
        tier.Property(t => t.Capacity);
        tier.Property(t => t.IsInGame);
    }
}