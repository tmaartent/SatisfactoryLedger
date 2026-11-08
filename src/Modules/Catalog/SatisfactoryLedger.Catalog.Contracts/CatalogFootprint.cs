namespace SatisfactoryLedger.Catalog.Contracts;

/// <summary>The ground a building covers, seen from above: a rectangle in its own coordinates, in centimetres.</summary>
/// <param name="Id">The building's class name, e.g. <c>Build_ConstructorMk1_C</c>.</param>
public sealed record CatalogFootprint(string Id, decimal MinX, decimal MinY, decimal MaxX, decimal MaxY);