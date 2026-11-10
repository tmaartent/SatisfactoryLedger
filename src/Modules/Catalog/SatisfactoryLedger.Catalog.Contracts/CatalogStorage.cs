namespace SatisfactoryLedger.Catalog.Contracts;

/// <summary>What a building with an inventory holds (BR-A7): slots of items, or a volume of fluid.</summary>
/// <param name="Id">The building's class name, e.g. <c>Build_StorageContainerMk2_C</c>.</param>
/// <param name="Slots">Its inventory slots; 0 for a fluid buffer.</param>
/// <param name="FluidCubicMetres">The fluid it holds in m³; 0 for an inventory of slots.</param>
public sealed record CatalogStorage(string Id, int Slots, decimal FluidCubicMetres);
