using SatisfactoryLedger.World.Factories;
using SatisfactoryLedger.World.Snapshots;

using static SatisfactoryLedger.World.Tests.TestWorldSnapshot;

namespace SatisfactoryLedger.World.Tests;

/// <summary>
/// A factory definition traced in a stored snapshot (BR-W3, BR-W4, BR-W11).
/// </summary>
public class FactoryTracesTests
{
    private static StoredMachine Machine(string id) => StoredMachine.From(Document.Buildables.Single(buildable => buildable.Id == id));

    private static StoredFactory IronPlates() => new()
    {
        Id = Guid.CreateVersion7(),
        World = "Development",
        Name = "Iron plates",
        Machines = [Machine(SmelterA), Machine(SmelterB), Machine(ConstructorC)],
    };

    private static FactoryTrace Trace(SnapshotDocument snapshot, StoredFactory factory) => FactoryTraces.TraceAll(snapshot, [factory]).Single();

    [Fact]
    public void A_factory_traced_in_a_snapshot_lists_its_machines_with_their_class_and_recipe()
    {
        FactoryTrace trace = Trace(Document, IronPlates());

        Assert.True(trace.Traceable);
        Assert.Equal(
            [(ConstructorC, "Build_ConstructorMk1_C", "Recipe_IronPlate_C"), (SmelterA, "Build_SmelterMk1_C", "Recipe_IngotIron_C"), (SmelterB, "Build_SmelterMk1_C", "Recipe_IngotIron_C")],
            trace.Machines.Select(machine => (machine.Id, machine.ClassName, machine.Recipe)));
    }

    [Fact]
    public void A_machine_two_factories_have_is_owned_by_the_oldest_in_both_traces_whatever_their_names()
    {
        StoredFactory oldest = new() { Id = Guid.Parse("019ac000-0000-7000-8000-000000000001"), World = "Development", Name = "Zinc-free plates", Machines = [Machine(SmelterA), Machine(SmelterB)] };
        StoredFactory younger = new() { Id = Guid.Parse("019ac000-0000-7000-8000-000000000002"), World = "Development", Name = "Alpha ingots", Machines = [Machine(SmelterA)] };

        IReadOnlyList<FactoryTrace> traces = FactoryTraces.TraceAll(Document, [younger, oldest]);

        Assert.All(traces, trace => Assert.Equal(oldest.Id, trace.Machines.Single(machine => machine.Id == SmelterA).Owner));
        Assert.Equal(oldest.Id, traces.Single(trace => trace.FactoryId == oldest.Id).Machines.Single(machine => machine.Id == SmelterB).Owner);
    }

    [Fact]
    public void A_factory_traced_in_a_snapshot_lists_its_outputs_and_inputs_with_their_items()
    {
        FactoryTrace trace = Trace(Document, IronPlates());

        Assert.Equal(
            [("Build_StorageContainerMk2_C", (string?)null), ("Build_ConveyorBeltMk1_C", "Build_ResourceSink_C")],
            trace.Outputs.Select(output => (output.Inside.ClassName, output.Outside?.ClassName)).OrderBy(output => output.Item2, StringComparer.Ordinal));
        Assert.All(trace.Outputs, output => Assert.Equal(["Desc_IronPlate_C"], output.Items));
        ConnectionSummary input = Assert.Single(trace.Inputs);
        Assert.Equal(("Build_ConveyorBeltMk1_C", Miner), (input.Inside.ClassName, input.Outside?.Id));
        Assert.Equal(["Desc_OreIron_C"], input.Items);
    }

    [Fact]
    public void A_preview_of_chosen_machines_says_which_logistics_they_would_own_and_what_they_hand_over()
    {
        FactoryPreview preview = FactoryTraces.Preview(Document, [SmelterA, SmelterB, ConstructorC], analysis: null);

        Assert.Contains(BeltIntoContainer, preview.Logistics);
        Assert.Contains(preview.Outputs, output => output.Items.Contains("Desc_IronPlate_C"));
        Assert.Contains(preview.Inputs, input => input.Items.Contains("Desc_OreIron_C"));
    }

    [Fact]
    public void A_preview_of_ids_that_are_no_machines_owns_and_hands_over_nothing()
    {
        FactoryPreview preview = FactoryTraces.Preview(Document, [BeltIntoContainer, Id("Build_SmelterMk1_C_1")], analysis: null);

        Assert.Equal((0, 0, 0), (preview.Logistics.Count, preview.Outputs.Count, preview.Inputs.Count));
    }

    [Fact]
    public void A_preview_says_per_item_what_leaves_and_arrives_now_by_the_stored_analysis()
    {
        FactoryPreview preview = FactoryTraces.Preview(Document, [SmelterA, SmelterB, ConstructorC], TestWorldAnalysis);
        Assert.Null(preview.Power);

        Assert.Equal([new PreviewItem("Desc_IronPlate_C", 15)], preview.Leaving);
        Assert.Equal("Desc_OreIron_C", Assert.Single(preview.Arriving).Item);
    }

    [Fact]
    public void A_preview_before_the_analysis_is_ready_names_the_items_without_rates()
    {
        FactoryPreview preview = FactoryTraces.Preview(Document, [SmelterA, SmelterB, ConstructorC], analysis: null);

        Assert.Equal([new PreviewItem("Desc_IronPlate_C", null)], preview.Leaving);
    }
}
