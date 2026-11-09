using System.Reflection;


namespace SatisfactoryLedger.ArchitectureTests;

internal static class SolutionAssemblies
{
    private static readonly Assembly CatalogDomain = typeof(Catalog.Domain.CatalogDomainAssembly).Assembly;
    private static readonly Assembly WorldDomain = typeof(World.Domain.WorldDomainAssembly).Assembly;

    private static readonly Assembly CatalogContracts = typeof(Catalog.Contracts.CatalogContractsAssembly).Assembly;
    private static readonly Assembly WorldContracts = typeof(World.Contracts.WorldContractsAssembly).Assembly;
    
    private static readonly Assembly CatalogImplementation = typeof(Catalog.CatalogModule).Assembly;
    private static readonly Assembly WorldImplementation = typeof(World.WorldModule).Assembly;
    
    public static readonly Assembly[] Domain = [CatalogDomain, WorldDomain];
    public static readonly Assembly[] Contracts = [CatalogContracts, WorldContracts];
    private static readonly Assembly[] Implementation = [CatalogImplementation, WorldImplementation];

    /// <summary>Anti-corruption layers around a third-party library, part of their module (ADR-0013).</summary>
    public static readonly Assembly WorldSaveFile = typeof(World.SaveFile.SaveFileReader).Assembly;
    private static readonly Assembly[] Adapters = [WorldSaveFile];

    public static readonly Assembly[] All = [..Domain, ..Contracts, ..Implementation, ..Adapters];
    
}