using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.Persistence.Configurations;

internal sealed class GeneratorConfiguration : IEntityTypeConfiguration<Generator>
{
    public void Configure(EntityTypeBuilder<Generator> generator)
    {
        generator.HasKey(g => g.Id);
        generator.Property(g => g.Id).HasConversion(id => id.Id, value => GeneratorId.Of(value));
        generator.Property(g => g.Name);
        generator.Property(g => g.PowerMegawatts);
        generator.Property(g => g.SupplementalLitresPerMegajoule);
        generator.Property(g => g.IsInGame);

        // The fuels only exist inside their generator: one jsonb column, mapped through the private List<T> field.
        generator.Ignore(g => g.Fuels);
        generator.ComplexCollection<List<GeneratorFuel>, GeneratorFuel>(FuelsField, fuel =>
        {
            fuel.ToJson("fuels");
            fuel.Property(f => f.Fuel).HasConversion(id => id.Id, value => ItemId.Of(value));
            fuel.Property(f => f.EnergyMegajoules);
            fuel.Property(f => f.Supplemental).HasConversion(id => id!.Id, value => ItemId.Of(value));
            fuel.Property(f => f.Byproduct).HasConversion(id => id!.Id, value => ItemId.Of(value));
            fuel.Property(f => f.ByproductPerFuel);
        });
    }

    public const string FuelsField = "_fuels";
}