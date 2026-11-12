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
    public void A_machine_rebuilt_within_a_metre_of_where_it_stood_is_still_the_factorys()
    {
        BuildableDocument smelter = Find(SmelterA);
        SnapshotDocument rebuilt = Replaced(smelter, smelter with
        {
            Id = Id("Build_SmelterMk1_C_1"),
            Position = smelter.Position! with { X = smelter.Position.X + 60, Y = smelter.Position.Y + 60 },
        });

        FactoryTrace trace = Trace(rebuilt, IronPlates());

        Assert.Contains(Id("Build_SmelterMk1_C_1"), trace.Machines.Select(machine => machine.Id));
        Assert.Empty(trace.Missing);
    }

    [Fact]
    public void A_machine_rebuilt_further_away_is_missing()
    {
        BuildableDocument smelter = Find(SmelterA);
        SnapshotDocument moved = Replaced(smelter, smelter with
        {
            Id = Id("Build_SmelterMk1_C_1"),
            Position = smelter.Position! with { X = smelter.Position.X + 150 },
        });

        FactoryTrace trace = Trace(moved, IronPlates());

        Assert.Equal([SmelterA], trace.Missing.Select(machine => machine.Id));
        Assert.Equal([ConstructorC, SmelterB], trace.Machines.Select(machine => machine.Id));
    }

    [Fact]
    public void A_machine_rebuilt_as_another_class_is_missing()
    {
        BuildableDocument smelter = Find(SmelterA);
        SnapshotDocument other = Replaced(smelter, smelter with { Id = Id("Build_FoundryMk1_C_1"), ClassName = "Build_FoundryMk1_C" });

        Assert.Equal([SmelterA], Trace(other, IronPlates()).Missing.Select(machine => machine.Id));
    }

    [Fact]
    public void A_factory_none_of_whose_machines_is_in_the_snapshot_is_not_traceable_there()
    {
        StoredFactory gone = IronPlates();
        gone.Machines = [new StoredMachine(Id("Build_SmelterMk1_C_1"), "Build_SmelterMk1_C", 0, 0, 0)];

        FactoryTrace trace = Trace(Document, gone);

        Assert.False(trace.Traceable);
        Assert.Empty(trace.Machines);
        Assert.Equal([Id("Build_SmelterMk1_C_1")], trace.Missing.Select(machine => machine.Id));
    }

    private static BuildableDocument Find(string id) => Document.Buildables.Single(buildable => buildable.Id == id);

    /// <summary>The snapshot with one buildable replaced by another, connected the same way.</summary>
    private static SnapshotDocument Replaced(BuildableDocument old, BuildableDocument replacement)
    {
        string Swap(string id) => id == old.Id ? replacement.Id : id;
        return Document with
        {
            Buildables = [.. Document.Buildables.Select(buildable => buildable.Id == old.Id ? replacement : buildable)],
            Connections = [.. Document.Connections.Select(connection => connection with { From = Swap(connection.From), To = Swap(connection.To) })],
        };
    }
}
