using System.Reflection;

namespace SatisfactoryLedger.ArchitectureTests;

internal static class SolutionAssemblies
{
    public static readonly Assembly CatalogDomain = typeof(Catalog.Domain.CatalogDomainAssembly).Assembly;
    public static readonly Assembly BlueprintDesignDomain = typeof(BlueprintDesign.Domain.BlueprintDesignDomainAssembly).Assembly;
    public static readonly Assembly FactoryNetworkDomain = typeof(FactoryNetwork.Domain.FactoryNetworkDomainAssembly).Assembly;
    
    public static readonly Assembly[] Domain = [CatalogDomain, BlueprintDesignDomain, FactoryNetworkDomain];
    
    public static readonly Assembly[] All = [.. Domain];
    
}