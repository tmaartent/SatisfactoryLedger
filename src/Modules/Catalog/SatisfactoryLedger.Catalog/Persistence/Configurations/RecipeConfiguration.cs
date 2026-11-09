using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.Persistence.Configurations;

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