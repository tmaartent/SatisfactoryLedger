using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.Persistence.Configurations;

internal sealed class PumpConfiguration : IEntityTypeConfiguration<Pump>
{
    public void Configure(EntityTypeBuilder<Pump> pump)
    {
        pump.HasKey(p => p.Id);
        pump.Property(p => p.Id).HasConversion(id => id.Id, value => PumpId.Of(value));
        pump.Property(p => p.Name);
        pump.Property(p => p.HeadLiftMetres);
        pump.Property(p => p.IsInGame);
    }
}
