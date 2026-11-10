using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.Persistence.Configurations;

internal sealed class StorageConfiguration : IEntityTypeConfiguration<Storage>
{
    public void Configure(EntityTypeBuilder<Storage> storage)
    {
        storage.HasKey(s => s.Id);
        storage.Property(s => s.Id).HasConversion(id => id.Id, value => StorageId.Of(value));
        storage.Property(s => s.Name);
        storage.Property(s => s.Slots);
        storage.Property(s => s.FluidCubicMetres);
        storage.Property(s => s.IsInGame);
    }
}
