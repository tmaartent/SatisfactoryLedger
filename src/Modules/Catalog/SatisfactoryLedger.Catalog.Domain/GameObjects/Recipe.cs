using SatisfactoryLedger.Catalog.Domain.Import;
using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

public class Recipe : ICatalogEntry<Recipe>
{
    // EF Core stores these fields (it needs a mutable List<T>); the domain only hands out read-only views.
    private List<RecipeIngredient> _inputs = [];
    private List<RecipeIngredient> _outputs = [];

    // EF Core can only pass scalar properties to a constructor; it fills _inputs and _outputs afterwards.
    private Recipe(RecipeId id, string name, BuildingId buildingId, decimal cycleSeconds, bool isAlternate)
    {
        Id = id;
        Name = name;
        BuildingId = buildingId;
        CycleSeconds = cycleSeconds;
        IsAlternate = isAlternate;
    }

    public static Recipe Create(RecipeId id, string name, BuildingId buildingId, decimal cycleSeconds,
        RecipeIngredient[] inputs,
        RecipeIngredient[] outputs, bool isAlternate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cycleSeconds);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArrayArgumentException.ThrowIfLessThan(inputs, 0);
        ArrayArgumentException.ThrowIfMoreThan(inputs, 4);
        ArrayArgumentException.ThrowIfLessThan(outputs, 1);
        ArrayArgumentException.ThrowIfMoreThan(outputs, 2);

        return new Recipe(id, name, buildingId, cycleSeconds, isAlternate)
        {
            _inputs = [.. inputs],
            _outputs = [.. outputs]
        };
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

    public string Name { get; private set; }

    public BuildingId BuildingId { get; private set; }

    public decimal CycleSeconds { get; private set; }

    public IReadOnlyList<RecipeIngredient> Inputs => _inputs;
    
    public IReadOnlyList<RecipeIngredient> Outputs => _outputs;
    
    public bool IsAlternate { get; private set; }

    public ItemRate[] BaseInputRates()
    {
        return ToItemRates(Inputs);
    }

    private ItemRate[] ToItemRates(IEnumerable<RecipeIngredient> ingredients)
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

    public bool IsInGame { get; private set; } = true;

    public void UpdateFromGame(Recipe fromGame)
    {
        if (!Equals(fromGame))
        {
            throw new ArgumentException($"{fromGame} is a different recipe than {this}", nameof(fromGame));
        }

        Name = fromGame.Name;
        BuildingId = fromGame.BuildingId;
        CycleSeconds = fromGame.CycleSeconds;
        _inputs = [.. fromGame._inputs];
        _outputs = [.. fromGame._outputs];
        IsAlternate = fromGame.IsAlternate;
        IsInGame = true;
    }

    public void MarkNoLongerInGame()
    {
        IsInGame = false;
    }

    public override string ToString() => Id.Id;
}