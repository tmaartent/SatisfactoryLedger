using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using Type = System.Type;

namespace SatisfactoryLedger.ArchitectureTests;
    

public class ModuleBoundaryTests
{
    
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(SolutionAssemblies.All)
        .Build();

    private static readonly IObjectProvider<IType> DomainLayers =
        Types().That()
            .ResideInAssembly(SolutionAssemblies.Domain[0], SolutionAssemblies.Domain[1..])
            .As("Domain layers");

    private static readonly IObjectProvider<IType> EntityFrameworkCore =
        Types(true).That().ResideInNamespaceMatching(@"^Microsoft\.EntityFrameworkCore(\.|$)").As("Entity Framework Core layer");
    
    private static readonly IObjectProvider<IType> AspNetCore =
        Types(true).That().ResideInNamespaceMatching(@"^Microsoft\.AspNetCore(\.|$)").As("ASP.NET Core layer");

    

    [Fact]
    public void Domain_layers_do_not_use_the_EntityFramework()
    {
        Types().That().Are(DomainLayers)
            .Should().NotDependOnAny(EntityFrameworkCore)
            .Because("the domain layers should not use the EntityFramework Core")
            .Check(Architecture);
    }
    
    [Fact]
    public void Domain_layers_do_not_use_the_ASPNETCore()
    {
        Types().That().Are(DomainLayers)
            .Should().NotDependOnAny(AspNetCore)
            .Because("the domain layers should not use ASP.NET Core")
            .Check(Architecture);
    }

    [Theory]
    [InlineData(typeof(Catalog.Contracts.CatalogContractsAssembly))]
    [InlineData(typeof(BlueprintDesign.Contracts.BlueprintDesignContractsAssembly))]
    [InlineData(typeof(FactoryNetwork.Contracts.FactoryNetworkContractsAssembly))]
    public void Contract_Layers_have_no_dependencies_on_other_project_assemblies(Type contractsMarker)
    {
        var contracts = contractsMarker.Assembly;

        var forbidden = Types().That().ResideInAssembly(SolutionAssemblies.All[0], SolutionAssemblies.All[1..])
            .And()
            .DoNotResideInAssembly(contracts);
            
        Types().That().ResideInAssembly(contracts).As(contracts.GetName().Name)
            .Should().NotDependOnAny(forbidden)
            .Because("the contract layers have no dependencies on other project assemblies")
            .Check(Architecture);
    }

    [Theory]
    [InlineData(typeof(Catalog.CatalogModule))]
    [InlineData(typeof(BlueprintDesign.BlueprintDesignModule))]
    [InlineData(typeof(FactoryNetwork.FactoryNetworkModule))]
    public void Implementation_types_are_not_public_except_the_module_entry_point(Type implementationMarker) 
    {
        var implementations = implementationMarker.Assembly;
        
        Types().That().ResideInAssembly(implementations)
            .And().AreNot(implementationMarker)
            .As(implementations.GetName().Name)
            .Should().NotBePublic()
            .Because("implementation classes should be internal except for the Module Entrypoint class")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }
    
    [Theory]
    [InlineData(typeof(Catalog.CatalogModule), typeof(Catalog.Domain.CatalogDomainAssembly))]
    [InlineData(typeof(BlueprintDesign.BlueprintDesignModule), typeof(BlueprintDesign.Domain.BlueprintDesignDomainAssembly))]
    [InlineData(typeof(FactoryNetwork.FactoryNetworkModule), typeof(FactoryNetwork.Domain.FactoryNetworkDomainAssembly))]
    public void Implementation_modules_only_depend_on_other_modules_contracts(Type implementationMarker, Type domainMarker) 
    {
        var ownImplementations = implementationMarker.Assembly;
        var ownDomain = domainMarker.Assembly;
        
        var forbidden = Types().That().ResideInAssembly(SolutionAssemblies.All[0], SolutionAssemblies.All[1..])
            .And()
            .DoNotResideInAssembly(SolutionAssemblies.Contracts[0], SolutionAssemblies.Contracts[1..])
            .And()
            .DoNotResideInAssembly(ownImplementations)
            .And()
            .DoNotResideInAssembly(ownDomain);
        
        Types().That().ResideInAssembly(ownImplementations)
            .As(ownImplementations.GetName().Name)
            .Should().NotDependOnAny(forbidden)
            .Because("modules talk to each other only through Contracts")
            .Check(Architecture);
    }
}