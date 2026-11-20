using ModelContextProtocol;

using SatisfactoryLedger.Mcp.LedgerApi;

namespace SatisfactoryLedger.Mcp.Catalog;

/// <summary>
/// The Catalog's names: the API speaks in class names (<c>Desc_IronPlate_C</c>), a model and a player in the names the
/// game shows (<c>Iron Plate</c>). Turns one into the other, both ways.
/// </summary>
internal sealed class GameData(IReadOnlyList<ItemResponse> items, IReadOnlyList<BuildableResponse> buildables, IReadOnlyList<RecipeResponse> recipes)
{
    /// <summary>How many names a failed lookup suggests.</summary>
    private const int Suggestions = 10;

    private readonly Dictionary<string, string> _items = items.ToDictionary(item => item.Id, item => item.Name, StringComparer.Ordinal);
    private readonly Dictionary<string, string> _buildings = buildables.ToDictionary(building => building.Id, building => building.Name, StringComparer.Ordinal);
    private readonly Dictionary<string, string> _recipes = recipes.ToDictionary(recipe => recipe.Id, recipe => recipe.Name, StringComparer.Ordinal);

    public IReadOnlyList<ItemResponse> Items { get; } = items;

    /// <summary>An item's in-game name; its class name when the Catalog doesn't know it.</summary>
    public string Item(string id) => _items.GetValueOrDefault(id, id);

    public string Building(string id) => _buildings.GetValueOrDefault(id, id);

    public string? Recipe(string? id) => id is null ? null : _recipes.GetValueOrDefault(id, id);

    /// <summary>The recipes that make an item, by their class names.</summary>
    public IReadOnlySet<string> RecipesMaking(string itemId) =>
        recipes.Where(recipe => recipe.Outputs.Any(output => output.ItemId == itemId)).Select(recipe => recipe.Id).ToHashSet(StringComparer.Ordinal);

    /// <summary>The items whose name holds the words asked, by name.</summary>
    public IReadOnlyList<ItemResponse> ItemsLike(string words) =>
        [.. Items.Where(item => item.Name.Contains(words.Trim(), StringComparison.OrdinalIgnoreCase)).OrderBy(item => item.Name, StringComparer.Ordinal)];

    /// <summary>The item meant by a name or class name; an error naming the likely ones when it isn't clear which.</summary>
    public ItemResponse FindItem(string name) => Find(Items, name, item => item.Id, item => item.Name, "item");

    public BuildableResponse FindBuilding(string name) => Find(buildables, name, building => building.Id, building => building.Name, "building");

    /// <summary>A recipe of one building, by its name or class name; an error naming the building's recipes when it isn't one of them.</summary>
    public RecipeResponse FindRecipe(BuildableResponse building, string name)
    {
        List<RecipeResponse> its = [.. recipes.Where(recipe => recipe.BuildingId == building.Id)];
        RecipeResponse? found = its.FirstOrDefault(recipe => string.Equals(recipe.Name, name.Trim(), StringComparison.OrdinalIgnoreCase) || recipe.Id == name.Trim());
        return found ?? throw new McpException(its.Count == 0
            ? $"The {building.Name} has no recipes."
            : $"The {building.Name} has no recipe \"{name.Trim()}\". Its recipes are: {string.Join(", ", its.Select(recipe => recipe.Name).Order(StringComparer.Ordinal))}.");
    }

    /// <summary>
    /// Exactly that name or class name, ignoring case; else the only name holding it. Anything else is an error a model
    /// can act on: the names it may have meant.
    /// </summary>
    private static T Find<T>(IEnumerable<T> all, string asked, Func<T, string> id, Func<T, string> name, string what)
    {
        string wanted = asked.Trim();
        List<T> candidates = [.. all];
        T? exact = candidates.FirstOrDefault(c => string.Equals(name(c), wanted, StringComparison.OrdinalIgnoreCase) || id(c) == wanted);
        if (exact is not null)
        {
            return exact;
        }

        List<T> like = [.. candidates.Where(c => name(c).Contains(wanted, StringComparison.OrdinalIgnoreCase)).OrderBy(name, StringComparer.Ordinal)];
        return like.Count switch
        {
            1 => like[0],
            0 => throw new McpException($"No {what} is called \"{wanted}\". Use its name as the game shows it."),
            _ => throw new McpException(
                $"\"{wanted}\" could be several: {string.Join(", ", like.Take(Suggestions).Select(name))}{(like.Count > Suggestions ? ", …" : "")}. Ask again with the full name."),
        };
    }
}
