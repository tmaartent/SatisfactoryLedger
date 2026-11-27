using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Factories;
using SatisfactoryLedger.World.Snapshots;

using static SatisfactoryLedger.World.Tests.TestWorldSnapshot;

namespace SatisfactoryLedger.World.Tests;

/// <summary>The stored factories of a world, matched in one of its snapshots (BR-W5, BR-W11).</summary>
public class SnapshotFactoriesTests
{
    private static StoredMachine Machine(string id) => StoredMachine.From(Document.Buildables.Single(buildable => buildable.Id == id));

    private static StoredFactory Factory(string name, params string[] machines) => new()
    {
        Id = Guid.CreateVersion7(),
        World = "Development",
        Name = name,
        Machines = [.. machines.Select(Machine)],
    };

    private static StoredFactory IronPlates() => Factory("Iron plates", SmelterA, SmelterB, ConstructorC);

    private static TracedFactory Traced(SnapshotDocument snapshot, StoredFactory factory) =>
        SnapshotFactories.Of(snapshot, [factory]).Factories.Single();

    private static IEnumerable<string> MachinesOf(TracedFactory traced) =>
        traced.Members.Machines.Select(id => id.Id).Order(StringComparer.Ordinal);

    [Fact]
    public void A_stored_machine_is_found_by_its_id()
    {
        TracedFactory traced = Traced(Document, IronPlates());

        Assert.True(traced.IsTraceable);
        Assert.Equal([ConstructorC, SmelterA, SmelterB], MachinesOf(traced));
        Assert.Empty(traced.Missing);
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

        TracedFactory traced = Traced(rebuilt, IronPlates());

        Assert.Contains(Id("Build_SmelterMk1_C_1"), MachinesOf(traced));
        Assert.Empty(traced.Missing);
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

        TracedFactory traced = Traced(moved, IronPlates());

        Assert.Equal([SmelterA], traced.Missing.Select(machine => machine.Id));
        Assert.Equal([ConstructorC, SmelterB], MachinesOf(traced));
    }

    [Fact]
    public void A_machine_rebuilt_as_another_class_is_missing()
    {
        BuildableDocument smelter = Find(SmelterA);
        SnapshotDocument other = Replaced(smelter, smelter with { Id = Id("Build_FoundryMk1_C_1"), ClassName = "Build_FoundryMk1_C" });

        Assert.Equal([SmelterA], Traced(other, IronPlates()).Missing.Select(machine => machine.Id));
    }

    [Fact]
    public void A_factory_none_of_whose_machines_is_in_the_snapshot_is_not_traceable_there()
    {
        StoredFactory gone = IronPlates();
        gone.Machines = [new StoredMachine(Id("Build_SmelterMk1_C_1"), "Build_SmelterMk1_C", 0, 0, 0)];

        TracedFactory traced = Traced(Document, gone);

        Assert.False(traced.IsTraceable);
        Assert.Equal([Id("Build_SmelterMk1_C_1")], traced.Missing.Select(machine => machine.Id));
    }

    [Fact]
    public void A_factorys_machines_in_the_snapshot_are_its_stored_machines_a_rebuilt_one_under_the_id_that_replaced_it()
    {
        BuildableDocument smelter = Find(SmelterB);
        SnapshotDocument rebuilt = Replaced(smelter, smelter with { Id = Id("Build_SmelterMk1_C_1") });

        TracedFactory traced = Traced(rebuilt, IronPlates());

        Assert.Equal([SmelterA, Id("Build_SmelterMk1_C_1"), ConstructorC], traced.Machines.Select(id => id.Id));
    }

    [Fact]
    public void A_traceable_factorys_members_include_the_logistics_between_its_machines()
    {
        TracedFactory traced = Traced(Document, IronPlates());

        Assert.Contains(BuildableId.Of(BeltIntoContainer), traced.Members.Logistics);
    }

    [Fact]
    public void A_machine_two_factories_have_belongs_to_the_first_of_them_given()
    {
        StoredFactory smelting = Factory("Smelting", SmelterA);
        StoredFactory plates = Factory("Plates", SmelterA, ConstructorC);

        SnapshotFactories factories = SnapshotFactories.Of(Document, [plates, smelting]);

        Assert.Equal(plates.Id, factories.Owners[BuildableId.Of(SmelterA)]);
    }

    [Fact]
    public void A_rebuilt_machine_belongs_to_the_factory_it_was_matched_to()
    {
        BuildableDocument smelter = Find(SmelterA);
        SnapshotDocument rebuilt = Replaced(smelter, smelter with { Id = Id("Build_SmelterMk1_C_1") });
        StoredFactory smelting = Factory("Smelting", SmelterA);

        SnapshotFactories factories = SnapshotFactories.Of(rebuilt, [Factory("Plates", ConstructorC), smelting]);

        Assert.Equal(smelting.Id, factories.Owners[BuildableId.Of(Id("Build_SmelterMk1_C_1"))]);
    }

    [Fact]
    public void A_factory_is_found_by_its_id()
    {
        StoredFactory smelting = Factory("Smelting", SmelterA);

        SnapshotFactories factories = SnapshotFactories.Of(Document, [Factory("Plates", ConstructorC), smelting]);

        Assert.Same(smelting, factories.Find(smelting.Id)?.Factory);
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
