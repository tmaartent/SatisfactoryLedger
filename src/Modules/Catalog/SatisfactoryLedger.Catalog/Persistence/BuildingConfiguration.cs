using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SatisfactoryLedger.Catalog.Domain;

namespace SatisfactoryLedger.Catalog.Persistence;

internal sealed class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> building)
    {
        building.HasKey(b => b.Id);
        building.Property(b => b.Id).HasConversion(id => id.Id, value => BuildingId.Of(value));
        building.Property(b => b.Name);
        building.Property(b => b.SomersloopSlots);
        building.Property(b => b.IsInGame);
    }
}