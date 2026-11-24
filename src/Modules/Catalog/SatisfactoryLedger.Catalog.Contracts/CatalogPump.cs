namespace SatisfactoryLedger.Catalog.Contracts;

/// <summary>A pipeline pump: past it, fluid rises up to <see cref="HeadLiftMetres"/> above it.</summary>
/// <param name="Id">The building's class name, e.g. <c>Build_PipelinePumpMk2_C</c>.</param>
public sealed record CatalogPump(string Id, decimal HeadLiftMetres);
