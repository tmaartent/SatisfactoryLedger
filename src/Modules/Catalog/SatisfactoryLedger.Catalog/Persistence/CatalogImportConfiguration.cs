using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SatisfactoryLedger.Catalog.Domain;

namespace SatisfactoryLedger.Catalog.Persistence;

internal sealed class CatalogImportConfiguration : IEntityTypeConfiguration<CatalogImport>
{
    public void Configure(EntityTypeBuilder<CatalogImport> import)
    {
        import.ToTable("imports");
        import.HasKey(i => i.Id);
        import.Property(i => i.Id).HasConversion(id => id.Id, value => CatalogImportId.Of(value));
        import.Property(i => i.Source).HasConversion<string>();
        import.Property(i => i.GameVersion);
        import.Property(i => i.ImportedAt);

        // Value objects without identity: their fields become columns of the imports table (items_added, ...).
        import.ComplexProperty(i => i.Items);
        import.ComplexProperty(i => i.Buildings);
        import.ComplexProperty(i => i.TransportTiers);
        import.ComplexProperty(i => i.Recipes);
        import.ComplexProperty(i => i.Extractors);
        import.ComplexProperty(i => i.Footprints);
        import.ComplexProperty(i => i.Generators);
        import.ComplexProperty(i => i.DronePorts);
        import.ComplexProperty(i => i.ResourceNodes);

        // A list of strings becomes a PostgreSQL text[] column.
        import.PrimitiveCollection(i => i.NoLongerInGame);
    }
}