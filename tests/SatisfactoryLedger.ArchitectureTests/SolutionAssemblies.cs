using System.Reflection;

namespace SatisfactoryLedger.ArchitectureTests;

internal static class SolutionAssemblies
{
    private static readonly Assembly CatalogDomain = typeof(Catalog.Domain.CatalogDomainAssembly).Assembly;
    private static readonly Assembly BlueprintDesignDomain = typeof(BlueprintDesign.Domain.BlueprintDesignDomainAssembly).Assembly;
    private static readonly Assembly FactoryNetworkDomain = typeof(FactoryNetwork.Domain.FactoryNetworkDomainAssembly).Assembly;

    private static readonly Assembly CatalogContracts = typeof(Catalog.Contracts.CatalogContractsAssembly).Assembly;
    private static readonly Assembly BlueprintDesignContracts = typeof(BlueprintDesign.Contracts.BlueprintDesignContractsAssembly).Assembly;
    private static readonly Assembly FactoryNetworkContracts = typeof(FactoryNetwork.Contracts.FactoryNetworkContractsAssembly).Assembly;
    
    private static readonly Assembly CatalogImplementation = typeof(Catalog.CatalogModule).Assembly;
    private static readonly Assembly BlueprintDesignImplementation = typeof(BlueprintDesign.BlueprintDesignModule).Assembly;
    private static readonly Assembly FactoryNetworkImplementation = typeof(FactoryNetwork.FactoryNetworkModule).Assembly;
    
    public static readonly Assembly[] Domain = [CatalogDomain, BlueprintDesignDomain, FactoryNetworkDomain];
    public static readonly Assembly[] Contracts = [CatalogContracts, BlueprintDesignContracts, FactoryNetworkContracts];
    private static readonly Assembly[] Implementation = [CatalogImplementation, BlueprintDesignImplementation, FactoryNetworkImplementation];
    
    public static readonly Assembly[] All = [..Domain, ..Contracts, ..Implementation];
    
}