using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.Persistence.Configurations;

internal sealed class BuildableConfiguration : IEntityTypeConfiguration<Buildable>
{
    public void Configure(EntityTypeBuilder<Buildable> buildable)
    {
        buildable.HasKey(b => b.Id);
        buildable.Property(b => b.Id).HasConversion(id => id.Id, value => BuildableId.Of(value));
        buildable.Property(b => b.Name);
        buildable.Property(b => b.IsInGame);
    }
}
