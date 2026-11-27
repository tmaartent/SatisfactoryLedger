using SatisfactoryLedger.World.Factories;
using SatisfactoryLedger.World.Snapshots;

using static SatisfactoryLedger.World.Tests.TestWorldSnapshot;

namespace SatisfactoryLedger.World.Tests;

/// <summary>A machine belongs to one factory at most (BR-W5): which chosen machines another factory already has.</summary>
public class ClaimedMachinesTests
{
    private static StoredFactory Smelting() => new()
    {
        Id = Guid.CreateVersion7(),
        World = "Development",
        Name = "Smelting",
        Machines = [StoredMachine.From(Document.Buildables.Single(buildable => buildable.Id == SmelterA))],
    };

    [Fact]
    public void A_machine_another_factory_has_is_taken_by_that_factory()
    {
        IReadOnlyList<TakenMachine> taken = ClaimedMachines.Among([SmelterA, ConstructorC], Guid.CreateVersion7(), Document, [Smelting()]);

        Assert.Equal([new TakenMachine(SmelterA, "Smelting")], taken);
    }

    [Fact]
    public void A_machine_no_factory_has_is_free()
    {
        Assert.Empty(ClaimedMachines.Among([SmelterB, ConstructorC], Guid.CreateVersion7(), Document, [Smelting()]));
    }

    [Fact]
    public void A_machine_rebuilt_within_a_metre_is_still_taken_by_the_factory_of_the_one_it_replaced()
    {
        BuildableDocument smelter = Document.Buildables.Single(buildable => buildable.Id == SmelterA);
        string rebuiltId = Id("Build_SmelterMk1_C_1");
        SnapshotDocument rebuilt = Document with
        {
            Buildables =
            [
                .. Document.Buildables.Select(buildable => buildable.Id == SmelterA
                    ? smelter with { Id = rebuiltId, Position = smelter.Position! with { X = smelter.Position.X + 50 } }
                    : buildable),
            ],
        };

        IReadOnlyList<TakenMachine> taken = ClaimedMachines.Among([rebuiltId], Guid.CreateVersion7(), rebuilt, [Smelting()]);

        Assert.Equal([new TakenMachine(rebuiltId, "Smelting")], taken);
    }

    [Fact]
    public void A_rebuilt_machine_the_edited_factory_is_matched_to_is_not_taken_by_a_younger_one_that_stands_closer()
    {
        BuildableDocument smelter = Document.Buildables.Single(buildable => buildable.Id == SmelterA);
        string rebuiltId = Id("Build_SmelterMk1_C_1");
        SnapshotDocument rebuilt = Document with
        {
            Buildables =
            [
                .. Document.Buildables.Select(buildable => buildable.Id == SmelterA
                    ? smelter with { Id = rebuiltId, Position = smelter.Position! with { X = smelter.Position.X + 50 } }
                    : buildable),
            ],
        };
        StoredFactory older = new()
        {
            Id = Guid.Parse("019ac000-0000-7000-8000-000000000001"),
            World = "Development",
            Name = "Smelting",
            Machines = [StoredMachine.From(smelter)],
        };
        StoredFactory younger = new()
        {
            Id = Guid.Parse("019ac000-0000-7000-8000-000000000002"),
            World = "Development",
            Name = "Plates",
            Machines = [new StoredMachine(Id("Build_SmelterMk1_C_9"), smelter.ClassName, smelter.Position!.X + 40, smelter.Position.Y, smelter.Position.Z)],
        };

        Assert.Empty(ClaimedMachines.Among([rebuiltId], older.Id, rebuilt, [older, younger]));
    }
}
