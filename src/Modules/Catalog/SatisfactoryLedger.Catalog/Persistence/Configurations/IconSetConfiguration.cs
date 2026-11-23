using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SatisfactoryLedger.Catalog.Domain.Icons;

namespace SatisfactoryLedger.Catalog.Persistence.Configurations;

/// <summary>
/// The icon set (ADR-0032): one row, and its icons in a table of their own, the images as <c>bytea</c>, keyed by their
/// descriptor. There being one at a time, the set's key is a shadow property, as the map art's.
/// </summary>
internal sealed class IconSetConfiguration : IEntityTypeConfiguration<IconSet>
{
    public void Configure(EntityTypeBuilder<IconSet> set)
    {
        set.ToTable("icon_set");
        set.Property<int>("Id");
        set.HasKey("Id");
        set.Property(s => s.GameVersion).HasMaxLength(50);
        set.OwnsMany(s => s.Icons, icon =>
        {
            icon.ToTable("icons");
            icon.WithOwner().HasForeignKey("IconSetId");
            icon.HasKey("IconSetId", nameof(Icon.ClassName));
            icon.Property(i => i.ClassName).HasMaxLength(200);
            icon.Property(i => i.ContentType).HasMaxLength(50);
        });
        set.Navigation(s => s.Icons).UsePropertyAccessMode(PropertyAccessMode.Property);
    }
}
