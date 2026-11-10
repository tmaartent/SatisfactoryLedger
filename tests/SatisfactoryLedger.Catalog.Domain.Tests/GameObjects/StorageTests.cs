using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.Domain.Tests.GameObjects;

/// <summary>What a building with an inventory holds, from the Docs file (BR-A7): slots of items, or a volume of fluid.</summary>
public class StorageTests
{
    private static Storage Container() => Storage.Create(StorageId.Of("Build_StorageContainerMk2_C"), "Storage Container Mk.2", slots: 48, fluidCubicMetres: 0);

    [Fact]
    public void A_storage_holds_slots_of_items()
    {
        Assert.Equal((48, 0m), (Container().Slots, Container().FluidCubicMetres));
    }

    [Fact]
    public void A_fluid_buffer_holds_a_volume_of_fluid()
    {
        Storage buffer = Storage.Create(StorageId.Of("Build_IndustrialTank_C"), "Industrial Fluid Buffer", slots: 0, fluidCubicMetres: 2400);

        Assert.Equal((0, 2400m), (buffer.Slots, buffer.FluidCubicMetres));
    }

    [Fact]
    public void A_storage_holds_something()
    {
        Assert.Throws<ArgumentException>(() => Storage.Create(StorageId.Of("Build_StorageContainerMk1_C"), "Storage Container", 0, 0));
    }

    [Fact]
    public void A_storage_from_a_newer_game_file_takes_its_size()
    {
        Storage stored = Container();
        stored.MarkNoLongerInGame();

        stored.UpdateFromGame(Storage.Create(StorageId.Of("Build_StorageContainerMk2_C"), "Storage Container Mk.2", 60, 0));

        Assert.Equal((60, true), (stored.Slots, stored.IsInGame));
    }
}
