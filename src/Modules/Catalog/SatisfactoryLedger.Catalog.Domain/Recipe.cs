using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.Catalog.Domain;

public class Recipe
{
    private Recipe(RecipeId id, string name, BuildingId buildingId, int cycleSeconds, RecipeIngredient[] inputs,
        RecipeIngredient[] outputs, bool isAlternate)
    {
        Id = id;
        Name = name;
        BuildingId = buildingId;
        CycleSeconds = cycleSeconds;
        Inputs = inputs;
        Outputs = outputs;
        IsAlternate = isAlternate;
    }

    public static Recipe Create(RecipeId id, string name, BuildingId buildingId, int cycleSeconds,
        RecipeIngredient[] inputs,
        RecipeIngredient[] outputs, bool isAlternate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cycleSeconds);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArrayArgumentException.ThrowIfLessThan(inputs, 1);
        ArrayArgumentException.ThrowIfMoreThan(inputs, 4);
        ArrayArgumentException.ThrowIfLessThan(outputs, 1);
        ArrayArgumentException.ThrowIfMoreThan(outputs, 2);

        return new Recipe(id, name, buildingId, cycleSeconds, inputs, outputs, isAlternate);
    }
    
    private bool Equals(Recipe other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return obj is not null && (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((Recipe)obj));
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public RecipeId Id { get; }

    public string Name { get; }

    public BuildingId BuildingId { get; }

    public int CycleSeconds { get; }

    public RecipeIngredient[] Inputs { get; }
    
    public RecipeIngredient[] Outputs { get; }
    
    public bool IsAlternate { get; }

    public ItemRate[] BaseInputRates()
    {
        return ToItemRates(Inputs);
    }

    private ItemRate[] ToItemRates(RecipeIngredient[] ingredients)
    {
        return
        [
            .. ingredients
                .Select(ingredient =>
                    ItemRate.Of(
                        ingredient.ItemId,
                        Rate.FromRecipe(ingredient.Amount, CycleSeconds))
                )
        ];
    }

    public ItemRate[] BaseOutputRates()
    {
        return ToItemRates(Outputs);
    }
}