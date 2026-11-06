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

internal sealed class TransportTierConfiguration : IEntityTypeConfiguration<TransportTier>
{
    public void Configure(EntityTypeBuilder<TransportTier> tier)
    {
        tier.HasKey(t => t.Id);
        tier.Property(t => t.Id).HasConversion(id => id.Id, value => TransportTierId.Of(value));
        tier.Property(t => t.Name);
        tier.Property(t => t.Form).HasConversion<string>();
        tier.Property(t => t.Capacity);
        tier.Property(t => t.IsInGame);
    }
}

internal sealed class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> recipe)
    {
        recipe.HasKey(r => r.Id);
        recipe.Property(r => r.Id).HasConversion(id => id.Id, value => RecipeId.Of(value));
        recipe.Property(r => r.Name);
        recipe.Property(r => r.BuildingId).HasConversion(id => id.Id, value => BuildingId.Of(value));
        recipe.Property(r => r.CycleSeconds);
        recipe.Property(r => r.IsAlternate);
        recipe.Property(r => r.IsInGame);

        // Inputs and outputs are value objects that only exist inside their recipe: one jsonb column each.
        // EF needs an IList<T>, so it maps the private List<T> fields; the read-only properties are not mapped.
        recipe.Ignore(r => r.Inputs);
        recipe.Ignore(r => r.Outputs);
        recipe.ComplexCollection<List<RecipeIngredient>, RecipeIngredient>(InputsField, ingredient => MapIngredient(ingredient, "inputs"));
        recipe.ComplexCollection<List<RecipeIngredient>, RecipeIngredient>(OutputsField, ingredient => MapIngredient(ingredient, "outputs"));
    }

    public const string InputsField = "_inputs";
    public const string OutputsField = "_outputs";

    private static void MapIngredient(ComplexCollectionBuilder<RecipeIngredient> ingredient, string column)
    {
        ingredient.ToJson(column);
        ingredient.Property(i => i.ItemId).HasConversion(id => id.Id, value => ItemId.Of(value));
        ingredient.Property(i => i.Amount);
    }
}

internal sealed class ExtractorConfiguration : IEntityTypeConfiguration<Extractor>
{
    public void Configure(EntityTypeBuilder<Extractor> extractor)
    {
        extractor.HasKey(x => x.Id);
        extractor.Property(x => x.Id).HasConversion(id => id.Id, value => ExtractorId.Of(value));
        extractor.Property(x => x.Name);
        extractor.Property(x => x.Form).HasConversion<string>();
        extractor.Property(x => x.AmountPerCycle);
        extractor.Property(x => x.CycleSeconds);
        extractor.Property(x => x.IsInGame);
        extractor.Ignore(x => x.ExtractionRate);
    }
}

internal sealed class FootprintConfiguration : IEntityTypeConfiguration<Footprint>
{
    public void Configure(EntityTypeBuilder<Footprint> footprint)
    {
        footprint.HasKey(f => f.Id);
        footprint.Property(f => f.Id).HasConversion(id => id.Id, value => FootprintId.Of(value));
        footprint.Property(f => f.MinX);
        footprint.Property(f => f.MinY);
        footprint.Property(f => f.MaxX);
        footprint.Property(f => f.MaxY);
        footprint.Property(f => f.IsInGame);
        footprint.Ignore(f => f.Width);
        footprint.Ignore(f => f.Length);
    }
}

internal sealed class ResourceNodeConfiguration : IEntityTypeConfiguration<ResourceNode>
{
    public void Configure(EntityTypeBuilder<ResourceNode> node)
    {
        node.HasKey(n => n.Id);
        node.Property(n => n.Id).HasConversion(id => id.Id, value => ResourceNodeId.Of(value));
        node.Property(n => n.Kind).HasConversion<string>();
        node.Property(n => n.Resource).HasConversion(id => id.Id, value => ItemId.Of(value));
        node.Property(n => n.Purity).HasConversion<string>();
        node.Property(n => n.IsInGame);
        // A value object: its fields become columns (position_x, position_y, position_z).
        node.ComplexProperty(n => n.Position);
        node.HasIndex(n => n.Resource);
    }
}

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
        import.ComplexProperty(i => i.ResourceNodes);

        // A list of strings becomes a PostgreSQL text[] column.
        import.PrimitiveCollection(i => i.NoLongerInGame);
    }
}