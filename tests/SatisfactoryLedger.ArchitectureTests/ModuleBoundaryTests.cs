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
    
    private static readonly IObjectProvider<IType> SaveFileLibrary =
        Types(true).That().ResideInNamespaceMatching(@"^SatisfactorySaveNet(\.|$)").As("SatisfactorySaveNet library");

    private static readonly IObjectProvider<IType> OrTools =
        Types(true).That().ResideInNamespaceMatching(@"^Google\.OrTools(\.|$)").As("OR-Tools library");

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
    [InlineData(typeof(World.Contracts.WorldContractsAssembly))]
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
    [InlineData(typeof(World.WorldModule))]
    public void Implementation_types_are_not_public_except_the_module_entry_point(Type implementationMarker) 
    {
        var implementations = implementationMarker.Assembly;
        
        Types().That().ResideInAssembly(implementations)
            .And().AreNot(implementationMarker)
            // `dotnet ef migrations add` always generates public classes; nothing outside the module uses them.
            .And().DoNotResideInNamespaceMatching(@"\.Persistence\.Migrations$")
            .As(implementations.GetName().Name)
            .Should().NotBePublic()
            .Because("implementation classes should be internal except for the Module Entrypoint class")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }
    
    [Theory]
    [InlineData(typeof(Catalog.CatalogModule))]
    [InlineData(typeof(World.WorldModule))]
    public void Implementation_modules_only_depend_on_other_modules_contracts(Type implementationMarker) 
    {
        var ownImplementations = implementationMarker.Assembly;
        // The module's own projects: SatisfactoryLedger.World plus SatisfactoryLedger.World.Domain, .Contracts, .SaveFile.
        string ownName = ownImplementations.GetName().Name!;
        var ownModule = SolutionAssemblies.All
            .Where(assembly => assembly == ownImplementations || assembly.GetName().Name!.StartsWith(ownName + ".", StringComparison.Ordinal))
            .ToArray();
        
        var forbidden = Types().That().ResideInAssembly(SolutionAssemblies.All[0], SolutionAssemblies.All[1..])
            .And()
            .DoNotResideInAssembly(SolutionAssemblies.Contracts[0], SolutionAssemblies.Contracts[1..])
            .And()
            .DoNotResideInAssembly(ownModule[0], ownModule[1..]);
        
        Types().That().ResideInAssembly(ownImplementations)
            .As(ownImplementations.GetName().Name)
            .Should().NotDependOnAny(forbidden)
            .Because("modules talk to each other only through Contracts")
            .Check(Architecture);
    }

    [Fact]
    public void Only_the_save_file_adapter_uses_the_save_file_library()
    {
        Types().That().ResideInAssembly(SolutionAssemblies.All[0], SolutionAssemblies.All[1..])
            .And().DoNotResideInAssembly(SolutionAssemblies.WorldSaveFile)
            .Should().NotDependOnAny(SaveFileLibrary)
            .Because("the save format stays behind the anti-corruption layer (ADR-0013)")
            .Check(Architecture);
    }

    // Without this, the rule above would also pass if the namespace pattern matched nothing at all.
    [Fact]
    public void The_save_file_reader_is_where_the_library_is_used()
    {
        Types().That().ResideInAssembly(SolutionAssemblies.WorldSaveFile).And().HaveName("SaveFileReader")
            .Should().DependOnAny(SaveFileLibrary)
            .Check(Architecture);
    }

    [Fact]
    public void Only_the_simulation_uses_OR_Tools()
    {
        Types().That().ResideInAssembly(SolutionAssemblies.All[0], SolutionAssemblies.All[1..])
            .And().DoNotResideInAssembly(SolutionAssemblies.WorldSimulation)
            .Should().NotDependOnAny(OrTools)
            .Because("the solver's library stays out of the domain and the API, as the save library does (ADR-0026)")
            .Check(Architecture);
    }

    // Without this, the rule above would also pass if the namespace pattern matched nothing at all.
    [Fact]
    public void The_linear_programme_is_where_OR_Tools_is_used()
    {
        Types().That().ResideInAssembly(SolutionAssemblies.WorldSimulation).And().HaveName("LinearProgramme")
            .Should().DependOnAny(OrTools)
            .Check(Architecture);
    }

    // ADR-0015: the map tool and CUE4Parse are run by hand and never become part of the app.
    [Fact]
    public void No_part_of_the_app_references_the_map_tool_or_CUE4Parse()
    {
        string[] forbidden = ["SatisfactoryLedger.MapExtractor", "CUE4Parse"];
        System.Reflection.Assembly[] app = [.. SolutionAssemblies.All, System.Reflection.Assembly.Load("SatisfactoryLedger.Api")];

        Assert.All(app, assembly => Assert.DoesNotContain(
            assembly.GetReferencedAssemblies(), reference => forbidden.Contains(reference.Name)));
    }
}
