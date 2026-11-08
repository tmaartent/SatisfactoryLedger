namespace SatisfactoryLedger.Catalog.Contracts;

/// <param name="Id">The building's class name, e.g. <c>Build_ConveyorBeltMk1_C</c>.</param>
/// <param name="CapacityPerMinute">Items per minute for a belt or lift, cubic metres per minute for a pipe.</param>
public sealed record CatalogTransportTier(string Id, int CapacityPerMinute);