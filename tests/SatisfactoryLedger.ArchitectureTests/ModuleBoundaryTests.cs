using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace SatisfactoryLedger.ArchitectureTests;
    

public class ModuleBoundaryTests
{
    
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(SolutionAssemblies.All)
        .Build();

    private static readonly IObjectProvider<IType> DomainLayers =
        Types().That()
            .ResideInAssembly(SolutionAssemblies.All[0], SolutionAssemblies.All[1..])
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
}