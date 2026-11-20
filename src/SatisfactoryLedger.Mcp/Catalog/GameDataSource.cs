using Microsoft.Extensions.Caching.Memory;

using SatisfactoryLedger.Mcp.LedgerApi;

namespace SatisfactoryLedger.Mcp.Catalog;

/// <summary>
/// The Catalog's names, read from the API once every few minutes: they change only with a Catalog import, and every
/// tool call needs them.
/// </summary>
internal sealed class GameDataSource(LedgerClient ledger, IMemoryCache cache)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public async Task<GameData> GetAsync(CancellationToken cancellationToken) =>
        (await cache.GetOrCreateAsync(typeof(GameData), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Lifetime;
            Task<IReadOnlyList<ItemResponse>> items = ledger.GetItemsAsync(cancellationToken);
            Task<IReadOnlyList<BuildableResponse>> buildables = ledger.GetBuildablesAsync(cancellationToken);
            Task<IReadOnlyList<RecipeResponse>> recipes = ledger.GetRecipesAsync(null, cancellationToken);
            return new GameData(await items, await buildables, await recipes);
        }))!;
}
