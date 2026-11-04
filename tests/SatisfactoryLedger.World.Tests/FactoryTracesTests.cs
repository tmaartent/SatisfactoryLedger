using SatisfactoryLedger.World.Factories;

using static SatisfactoryLedger.World.Tests.TestWorldSnapshot;

namespace SatisfactoryLedger.World.Tests;

/// <summary>A factory definition traced in a stored snapshot (BR-W3, BR-W11), and the belts the tool suggests (BR-W12).</summary>
public class FactoryTracesTests
{
    private static StoredFactory IronPlates() => new()
    {
        Id = Guid.CreateVersion7(),
        World = "Development",
        Name = "Iron plates",
        OutputBelt = BeltIntoContainer,
        InputItems = ["Desc_OreIron_C"],
    };

    [Fact]
    public void A_factory_traced_in_a_snapshot_lists_its_machines_with_their_class_and_recipe()
    {
        FactoryTrace trace = FactoryTraces.Trace(Document, IronPlates());

        Assert.True(trace.Traceable);
        Assert.Equal(
            [(ConstructorC, "Build_ConstructorMk1_C", "Recipe_IronPlate_C"), (SmelterA, "Build_SmelterMk1_C", "Recipe_IngotIron_C"), (SmelterB, "Build_SmelterMk1_C", "Recipe_IngotIron_C")],
            trace.Machines.Select(machine => (machine.Id, machine.ClassName, machine.Recipe)).OrderBy(machine => machine.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void A_factory_whose_output_belt_is_not_in_the_snapshot_is_not_traceable_there()
    {
        FactoryTrace trace = FactoryTraces.Trace(Without(BeltIntoContainer), IronPlates());

        Assert.False(trace.Traceable);
        Assert.Empty(trace.Machines);
    }

    [Fact]
    public void The_belt_into_the_storage_container_is_suggested_as_an_output_belt_with_iron_plates()
    {
        OutputBeltSuggestion suggestion = Assert.Single(OutputBeltSuggestions.For(Document), belt => belt.Belt == BeltIntoContainer);

        Assert.Equal("Build_ConveyorBeltMk1_C", suggestion.ClassName);
        Assert.Equal(["Desc_IronPlate_C"], suggestion.Items);
        Assert.Equal(["Build_StorageContainerMk2_C"], suggestion.Into.Select(into => into.ClassName));
        Assert.NotNull(suggestion.Position);
    }

    [Fact]
    public void A_suggestion_says_which_machines_a_factory_from_that_belt_would_contain()
    {
        OutputBeltSuggestion suggestion = Assert.Single(OutputBeltSuggestions.For(Document), belt => belt.Belt == BeltIntoContainer);

        Assert.Equal(
            new Dictionary<string, int> { ["Build_ConstructorMk1_C"] = 1, ["Build_SmelterMk1_C"] = 2 },
            suggestion.Machines);
    }

    [Fact]
    public void The_belts_into_the_sink_and_the_one_that_ends_in_the_air_are_suggested_too()
    {
        HashSet<string> suggested = [.. OutputBeltSuggestions.For(Document).Select(belt => belt.Belt)];

        Assert.Contains(Id("Build_ConveyorBeltMk1_C_2147281116"), suggested);
        Assert.Contains(Id("Build_ConveyorBeltMk6_C_2147274025"), suggested);
    }
}
