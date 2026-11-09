namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

public record RecipeId
{
    private RecipeId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    public string Id { get; }

    public static RecipeId Of(string id) => new(id);
}