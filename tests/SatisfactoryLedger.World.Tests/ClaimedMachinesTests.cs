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
        IReadOnlyList<TakenMachine> taken = ClaimedMachines.Among([SmelterA, ConstructorC], Document, [Smelting()]);

        Assert.Equal([new TakenMachine(SmelterA, "Smelting")], taken);
    }

    [Fact]
    public void A_machine_no_factory_has_is_free()
    {
        Assert.Empty(ClaimedMachines.Among([SmelterB, ConstructorC], Document, [Smelting()]));
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

        IReadOnlyList<TakenMachine> taken = ClaimedMachines.Among([rebuiltId], rebuilt, [Smelting()]);

        Assert.Equal([new TakenMachine(rebuiltId, "Smelting")], taken);
    }
}
