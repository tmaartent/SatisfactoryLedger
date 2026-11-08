namespace SatisfactoryLedger.Catalog.Contracts;

/// <summary>
/// A drone port: a round trip of its drone costs <see cref="TripEnergyMegajoules"/> plus
/// <see cref="TripEnergyPerMetreMegajoules"/> per metre flown, paid with fuel at its home port.
/// </summary>
/// <param name="Id">The building's class name, <c>Build_DroneStation_C</c>.</param>
public sealed record CatalogDronePort(string Id, decimal TripEnergyMegajoules, decimal TripEnergyPerMetreMegajoules, IReadOnlyList<CatalogDroneFuel> Fuels);