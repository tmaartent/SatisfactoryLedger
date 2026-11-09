using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.Persistence.Configurations;

internal sealed class DronePortConfiguration : IEntityTypeConfiguration<DronePort>
{
    public void Configure(EntityTypeBuilder<DronePort> port)
    {
        port.HasKey(p => p.Id);
        port.Property(p => p.Id).HasConversion(id => id.Id, value => DronePortId.Of(value));
        port.Property(p => p.Name);
        port.Property(p => p.TripEnergyMegajoules);
        port.Property(p => p.TripEnergyPerMetreMegajoules);
        port.Property(p => p.IsInGame);

        // Like a generator's fuels: one jsonb column, mapped through the private List<T> field.
        port.Ignore(p => p.Fuels);
        port.ComplexCollection<List<DroneFuel>, DroneFuel>(GeneratorConfiguration.FuelsField, fuel =>
        {
            fuel.ToJson("fuels");
            fuel.Property(f => f.Fuel).HasConversion(id => id.Id, value => ItemId.Of(value));
            fuel.Property(f => f.EnergyMegajoules);
        });
    }
}