namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>
/// BR-A7: what an inventory at the end of a line still takes. It takes everything until it's full, as in the game: an
/// item fits while a slot is empty or a stack of it isn't full yet.
/// </summary>
public class InventoryTests
{
    private static readonly ItemId Casing = ItemId.Of("Desc_AluminumCasing_C");
    private static readonly ItemId Plate = Items.IronPlate;
    private static readonly ItemId Water = ItemId.Of("Desc_Water_C");

    private static readonly Dictionary<ItemId, int> StackSizes = new() { [Casing] = 200, [Plate] = 200 };

    [Fact]
    public void An_inventory_with_an_empty_slot_takes_any_item()
    {
        var container = Inventory.OfSlots(48, new Dictionary<ItemId, decimal> { [Casing] = 200 * 47 }, StackSizes);

        Assert.True(container.Takes(Plate));
    }

    [Fact]
    public void An_inventory_whose_slots_are_all_full_stacks_takes_nothing()
    {
        var container = Inventory.OfSlots(48, new Dictionary<ItemId, decimal> { [Casing] = 200 * 48 }, StackSizes);

        Assert.False(container.Takes(Casing));
        Assert.False(container.Takes(Plate));
    }

    [Fact]
    public void A_full_inventory_still_takes_what_the_save_shows_arriving_as_fast_as_it_arrives()
    {
        var container = Inventory.OfSlots(48, new Dictionary<ItemId, decimal> { [Casing] = 200 * 48 }, StackSizes) with
        {
            Arriving = new Dictionary<ItemId, double> { [Casing] = 45 },
        };

        Assert.Equal((45.0, 0.0), (container.TakesPerMinute(Casing), container.TakesPerMinute(Plate)));
    }

    [Fact]
    public void An_inventory_with_room_takes_an_item_as_fast_as_it_comes()
    {
        var container = Inventory.OfSlots(48, new Dictionary<ItemId, decimal> { [Casing] = 200 * 47 }, StackSizes);

        Assert.Equal(double.PositiveInfinity, container.TakesPerMinute(Plate));
    }

    [Fact]
    public void An_inventory_without_an_empty_slot_still_takes_an_item_it_has_a_stack_of_that_isnt_full()
    {
        // 47 full stacks of casings and 150 plates: every slot is taken, but the plate stack has room for 50 more.
        var container = Inventory.OfSlots(48, new Dictionary<ItemId, decimal> { [Casing] = 200 * 47, [Plate] = 150 }, StackSizes);

        Assert.True(container.Takes(Plate));
        Assert.False(container.Takes(Casing));
    }

    [Fact]
    public void An_inventory_takes_an_item_whose_stack_size_isnt_known()
    {
        // A Catalog imported before stack sizes: nothing can be called full.
        var container = Inventory.OfSlots(48, new Dictionary<ItemId, decimal> { [Casing] = 200 * 48 }, new Dictionary<ItemId, int>());

        Assert.True(container.Takes(Casing));
    }

    [Fact]
    public void A_fluid_buffer_takes_fluid_until_it_holds_its_volume()
    {
        Assert.True(Inventory.OfFluid(2400, new Dictionary<ItemId, decimal> { [Water] = 2399 }).Takes(Water));
        Assert.False(Inventory.OfFluid(2400, new Dictionary<ItemId, decimal> { [Water] = 2400 }).Takes(Water));
    }

    [Fact]
    public void The_dimensional_depot_takes_an_item_until_it_holds_its_limit_of_it()
    {
        // The limit is the stack size times the depot's upgrades: 5 here, so 1,000 casings.
        var depot = Inventory.OfDepot(new Dictionary<ItemId, decimal> { [Casing] = 1000, [Plate] = 400 }, StackSizes, stacks: 5);

        Assert.False(depot.Takes(Casing));
        Assert.True(depot.Takes(Plate));
    }

    [Fact]
    public void The_depots_stacks_are_read_from_an_item_at_its_limit()
    {
        // The save doesn't store the depot's upgrades: an item at stack size × k shows them.
        Assert.Equal(5, Inventory.DepotStacks(new Dictionary<ItemId, decimal> { [Casing] = 1000, [Plate] = 400 }, StackSizes));
    }
}
