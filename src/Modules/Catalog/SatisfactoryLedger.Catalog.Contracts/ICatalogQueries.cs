namespace SatisfactoryLedger.Catalog.Contracts;

/// <summary>
/// What other modules may ask the Catalog (ADR-0003). Ids are the game's class names, so a module that reads them
/// from a save can look them up as they are.
/// </summary>
public interface ICatalogQueries
{
    /// <summary>The recipes with these ids that the Catalog knows, including ones no longer in the game. Unknown ids are left out.</summary>
    Task<IReadOnlyList<CatalogRecipe>> GetRecipesAsync(IReadOnlyCollection<string> recipeIds, CancellationToken cancellationToken);

    /// <summary>The items with these ids that the Catalog knows, including ones no longer in the game. Unknown ids are left out.</summary>
    Task<IReadOnlyList<CatalogItem>> GetItemsAsync(IReadOnlyCollection<string> itemIds, CancellationToken cancellationToken);

    /// <summary>The resource nodes with these names that the Catalog knows (ADR-0015). Unknown names, such as water volumes, are left out.</summary>
    Task<IReadOnlyList<CatalogResourceNode>> GetResourceNodesAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken);

    /// <summary>The extractors with these building class names that the Catalog knows. Unknown ids are left out.</summary>
    Task<IReadOnlyList<CatalogExtractor>> GetExtractorsAsync(IReadOnlyCollection<string> extractorIds, CancellationToken cancellationToken);

    /// <summary>The belts, lifts and pipes with these building class names that the Catalog knows. Unknown ids are left out.</summary>
    Task<IReadOnlyList<CatalogTransportTier>> GetTransportTiersAsync(IReadOnlyCollection<string> tierIds, CancellationToken cancellationToken);

    /// <summary>The footprints of the buildings with these class names that the Catalog knows (ADR-0020). Unknown ids, such as lifts, are left out.</summary>
    Task<IReadOnlyList<CatalogFootprint>> GetFootprintsAsync(IReadOnlyCollection<string> buildingIds, CancellationToken cancellationToken);

    /// <summary>The generators with these class names that the Catalog knows (BR-A8). Unknown ids are left out.</summary>
    Task<IReadOnlyList<CatalogGenerator>> GetGeneratorsAsync(IReadOnlyCollection<string> generatorIds, CancellationToken cancellationToken);

    /// <summary>The drone ports with these class names that the Catalog knows (BR-A16). Unknown ids are left out.</summary>
    Task<IReadOnlyList<CatalogDronePort>> GetDronePortsAsync(IReadOnlyCollection<string> dronePortIds, CancellationToken cancellationToken);
}

/// <summary>
/// A drone port: a round trip of its drone costs <see cref="TripEnergyMegajoules"/> plus
/// <see cref="TripEnergyPerMetreMegajoules"/> per metre flown, paid with fuel at its home port.
/// </summary>
/// <param name="Id">The building's class name, <c>Build_DroneStation_C</c>.</param>
public sealed record CatalogDronePort(string Id, decimal TripEnergyMegajoules, decimal TripEnergyPerMetreMegajoules, IReadOnlyList<CatalogDroneFuel> Fuels);

/// <param name="EnergyMegajoules">Per item.</param>
public sealed record CatalogDroneFuel(string Fuel, decimal EnergyMegajoules);

/// <summary>
/// A generator: it burns a fuel at <see cref="PowerMegawatts"/> ÷ the fuel's energy per second, and needs its
/// supplemental fluid at <see cref="PowerMegawatts"/> × <see cref="SupplementalLitresPerMegajoule"/> litres per second.
/// </summary>
/// <param name="Id">The building's class name, e.g. <c>Build_GeneratorNuclear_C</c>.</param>
public sealed record CatalogGenerator(string Id, decimal PowerMegawatts, decimal SupplementalLitresPerMegajoule, IReadOnlyList<CatalogGeneratorFuel> Fuels);

/// <param name="EnergyMegajoules">Per item, or per cubic metre of a fluid fuel.</param>
/// <param name="Supplemental">The fluid it needs besides the fuel (water); null when none.</param>
/// <param name="Byproduct">What burning one item leaves (uranium waste), <paramref name="ByproductPerFuel"/> of it; null when nothing.</param>
public sealed record CatalogGeneratorFuel(string Fuel, decimal EnergyMegajoules, string? Supplemental, string? Byproduct, decimal ByproductPerFuel);

/// <summary>The ground a building covers, seen from above: a rectangle in its own coordinates, in centimetres.</summary>
/// <param name="Id">The building's class name, e.g. <c>Build_ConstructorMk1_C</c>.</param>
public sealed record CatalogFootprint(string Id, decimal MinX, decimal MinY, decimal MaxX, decimal MaxY);

/// <param name="Id">The building's class name, e.g. <c>Build_MinerMk1_C</c>.</param>
/// <param name="AmountPerCycle">Items, or cubic metres for a fluid, on a normal-purity node at 100% clock speed.</param>
public sealed record CatalogExtractor(string Id, decimal AmountPerCycle, decimal CycleSeconds);

/// <param name="Id">The building's class name, e.g. <c>Build_ConveyorBeltMk1_C</c>.</param>
/// <param name="CapacityPerMinute">Items per minute for a belt or lift, cubic metres per minute for a pipe.</param>
public sealed record CatalogTransportTier(string Id, int CapacityPerMinute);

/// <param name="Name">The node's name, as the save uses it: <c>BP_ResourceNode35</c>.</param>
/// <param name="Kind"><c>resourceNode</c> or <c>wellSatellite</c>.</param>
/// <param name="Resource">The item's class name: <c>Desc_OreIron_C</c>.</param>
/// <param name="Purity"><c>impure</c>, <c>normal</c> or <c>pure</c> (BR-W9).</param>
public sealed record CatalogResourceNode(string Name, string Kind, string Resource, string Purity);

/// <param name="Id">The item's class name, e.g. <c>Desc_OreIron_C</c>.</param>
/// <param name="Name">The name the game shows, e.g. "Iron Ore".</param>
/// <param name="IsFluid">A liquid or gas: it goes over pipes, not belts.</param>
public sealed record CatalogItem(string Id, string Name, bool IsFluid);

/// <param name="Id">The recipe's class name, e.g. <c>Recipe_IronPlate_C</c>.</param>
/// <param name="BuildingId">The building that runs it, e.g. <c>Build_ConstructorMk1_C</c>.</param>
public sealed record CatalogRecipe(
    string Id,
    string BuildingId,
    decimal CycleSeconds,
    IReadOnlyList<CatalogIngredient> Inputs,
    IReadOnlyList<CatalogIngredient> Outputs);

/// <param name="ItemId">The item's class name, e.g. <c>Desc_IronPlate_C</c>.</param>
/// <param name="AmountPerCycle">Items per cycle, or cubic metres for a fluid.</param>
public sealed record CatalogIngredient(string ItemId, decimal AmountPerCycle);
