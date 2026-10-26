using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;

using static ArchUnitNET.Fluent.ArchRuleDefinition; // gives you Types(), Classes(), ...

namespace SatisfactoryLedger.ArchitectureTests;


public class LayerRulesTests
{
    // 1. LOAD — once, static. Load every assembly that appears in a rule:
    //    both the code you check AND the code it must not touch.
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(BlueprintDesign.Domain.BlueprintDesignDomainAssembly).Assembly,
            typeof(Catalog.Domain.CatalogDomainAssembly).Assembly,
            typeof(FactoryNetwork.Domain.FactoryNetworkDomainAssembly).Assembly
            )
        .Build();

    // Reusable selections. As(...) is the name shown in failure messages.
    private static readonly IObjectProvider<IType> DomainLayers =
        Types().That()
            .ResideInNamespace("SatisfactoryLedger.BlueprintDesign.Domain")
            .Or()
            .ResideInNamespace("SatisfactoryLedger.Catalog.Domain")
            .Or()
            .ResideInNamespace("SatisfactoryLedger.FactoryNetwork.Domain")
            .As("Domain layers");
    
    private static readonly IObjectProvider<IType> EntityFrameworkCore =
        Types().That().ResideInNamespace("Microsoft.EntityFrameworkCore").As("Entity Framework Core layer");
    
    private static readonly IObjectProvider<IType> ASPNETCore =
        Types().That().ResideInNamespace("Microsoft.AspNetCore").As("ASP.NET Core layer");

    [Fact]
    public void Domain_layer_does_not_use_the_EntityFramework()
    {
        Types(true).That().Are(DomainLayers)
            .Should().NotDependOnAny(EntityFrameworkCore)
            .Because("the domain layer should not use the EntityFramework Core or ASP.NET Core layer")
            .Check(Architecture);
        
        Types(true).That().Are(DomainLayers)
            .Should().NotDependOnAny(EntityFrameworkCore)
            .Because("the domain layer should not use the EntityFramework Core or ASP.NET Core layer")
            .Check(Architecture);
    }
}