using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SatisfactoryLedger.World.Factories;

internal sealed class StoredFactoryConfiguration : IEntityTypeConfiguration<StoredFactory>
{
    public void Configure(EntityTypeBuilder<StoredFactory> factory)
    {
        factory.ToTable("factories");
        factory.HasKey(f => f.Id);
        factory.Property(f => f.World);
        factory.Property(f => f.Name);
        factory.ComplexCollection(f => f.Machines, machine => machine.ToJson("machines"));
        factory.Property(f => f.OutputBelt);
        factory.PrimitiveCollection(f => f.InputItems);

        // BR-W11: within a world, factory names are unique.
        factory.HasIndex(f => new { f.World, f.Name }).IsUnique();
    }
}
