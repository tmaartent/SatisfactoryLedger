using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.Domain.Tests.GameObjects;

/// <summary>Anything the player places, with the name the game shows for it.</summary>
public class BuildableTests
{
    [Fact]
    public void A_buildable_is_known_by_the_name_the_game_shows()
    {
        Buildable platform = Buildable.Create(BuildableId.Of("Build_TrainDockingStation_C"), "Freight Platform");

        Assert.Equal("Freight Platform", platform.Name);
    }

    [Fact]
    public void A_buildable_has_a_name()
    {
        Assert.Throws<ArgumentException>(() => Buildable.Create(BuildableId.Of("Build_TrainDockingStation_C"), " "));
    }

    [Fact]
    public void A_buildable_from_a_newer_game_file_takes_its_name()
    {
        Buildable stored = Buildable.Create(BuildableId.Of("Build_TrainDockingStation_C"), "Train Docking Station");
        stored.MarkNoLongerInGame();

        stored.UpdateFromGame(Buildable.Create(BuildableId.Of("Build_TrainDockingStation_C"), "Freight Platform"));

        Assert.Equal(("Freight Platform", true), (stored.Name, stored.IsInGame));
    }
}
