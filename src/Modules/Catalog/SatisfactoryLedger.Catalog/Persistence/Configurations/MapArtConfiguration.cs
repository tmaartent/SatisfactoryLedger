using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SatisfactoryLedger.Catalog.Domain.Map;

namespace SatisfactoryLedger.Catalog.Persistence.Configurations;

/// <summary>
/// The map art (ADR-0020): one row, and its tiles in a table of their own, the images as <c>bytea</c>. The domain has
/// no id for it, there being one at a time: the key is a shadow property.
/// </summary>
internal sealed class MapArtConfiguration : IEntityTypeConfiguration<MapArt>
{
    public void Configure(EntityTypeBuilder<MapArt> art)
    {
        art.ToTable("map_art");
        art.Property<int>("Id");
        art.HasKey("Id");
        art.Property(a => a.GameVersion).HasMaxLength(50);
        // Get-only, so not mapped by convention: named here, EF Core passes them to the constructor.
        art.Property(a => a.X0);
        art.Property(a => a.Y0);
        art.Property(a => a.Width);
        art.Property(a => a.Height);
        art.Ignore(a => a.Bounds);
        art.OwnsMany(a => a.Tiles, tile =>
        {
            tile.ToTable("map_art_tiles");
            tile.WithOwner().HasForeignKey("MapArtId");
            tile.HasKey("MapArtId", nameof(MapArtTile.Column), nameof(MapArtTile.Row));
            tile.Property(t => t.ContentType).HasMaxLength(50);
        });
        art.Navigation(a => a.Tiles).UsePropertyAccessMode(PropertyAccessMode.Property);
    }
}
