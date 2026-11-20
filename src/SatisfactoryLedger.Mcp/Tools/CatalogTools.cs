using System.ComponentModel;

using ModelContextProtocol.Server;

using SatisfactoryLedger.Mcp.Answers;
using SatisfactoryLedger.Mcp.Catalog;
using SatisfactoryLedger.Mcp.LedgerApi;

namespace SatisfactoryLedger.Mcp.Tools;

/// <summary>The game's data from the Catalog: for a model to check against, never to guess.</summary>
[McpServerToolType]
internal sealed class CatalogTools(LedgerClient ledger, GameDataSource gameData)
{
    [McpServerTool(Name = "find_items", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("The game's items whose name holds the words given, with their class name and whether they're solid or fluid.")]
    public async Task<IReadOnlyList<ItemAnswer>> FindItemsAsync(
        [Description("Part of the in-game name, e.g. \"plate\".")] string name,
        CancellationToken cancellationToken = default) =>
        [.. (await gameData.GetAsync(cancellationToken)).ItemsLike(name).Select(i => new ItemAnswer(i.Id, i.Name, i.Form, i.IsRawResource))];

    [McpServerTool(Name = "get_recipes", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("The recipes that make an item: the building each runs in, and what it takes and makes per minute at 100% clock speed.")]
    public async Task<IReadOnlyList<RecipeAnswer>> GetRecipesAsync(
        [Description("The item, by its in-game name, e.g. \"Iron Plate\".")] string item,
        [Description("Whether to include alternate recipes; true when left out.")] bool includeAlternates = true,
        CancellationToken cancellationToken = default)
    {
        GameData game = await gameData.GetAsync(cancellationToken);
        ItemResponse made = game.FindItem(item);
        IngredientAnswer Ingredient(IngredientResponse i) => new(game.Item(i.ItemId), i.Rate.PerMinute);
        return
        [
            .. (await ledger.GetRecipesAsync(made.Id, cancellationToken))
                .Where(r => includeAlternates || !r.IsAlternate)
                .Select(r => new RecipeAnswer(r.Id, r.Name, game.Building(r.BuildingId), r.IsAlternate, [.. r.Inputs.Select(Ingredient)], [.. r.Outputs.Select(Ingredient)])),
        ];
    }

    [McpServerTool(Name = "get_transport_tiers", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("The belt, lift and pipe tiers, with what each carries at most per minute.")]
    public async Task<IReadOnlyList<TransportTierAnswer>> GetTransportTiersAsync(CancellationToken cancellationToken = default) =>
        [.. (await ledger.GetTransportTiersAsync(cancellationToken)).Select(t => new TransportTierAnswer(t.Name, t.Form, t.Capacity))];
}
