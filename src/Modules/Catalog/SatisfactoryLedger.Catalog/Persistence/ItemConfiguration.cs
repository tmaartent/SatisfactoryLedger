using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SatisfactoryLedger.Catalog.Domain;

namespace SatisfactoryLedger.Catalog.Persistence;

// The mapping lives here, not as attributes on the domain classes (chapter 04, "Persistence with EF Core").
// Properties without a setter (`Id { get; }`) are not mapped by convention, so every property is listed explicitly.

internal sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> item)
    {
        item.HasKey(i => i.Id);
        item.Property(i => i.Id).HasConversion(id => id.Id, value => ItemId.Of(value));
        item.Property(i => i.Name);
        item.Property(i => i.ItemForm).HasConversion<string>();
        item.Property(i => i.IsRawResource);
        item.Property(i => i.IsInGame);
    }
}