namespace SatisfactoryLedger.Catalog.Contracts;

/// <param name="Id">The item's class name, e.g. <c>Desc_OreIron_C</c>.</param>
/// <param name="Name">The name the game shows, e.g. "Iron Ore".</param>
/// <param name="IsFluid">A liquid or gas: it goes over pipes, not belts.</param>
/// <param name="StackSize">How many fit in one inventory slot (BR-A7); 0 for a fluid.</param>
public sealed record CatalogItem(string Id, string Name, bool IsFluid, int StackSize = 0);