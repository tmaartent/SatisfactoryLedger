namespace SatisfactoryLedger.Catalog.Domain.Tests;

public class ItemTests
{
    [Fact]
    public void Items_with_different_ids_are_not_equal()
    {
        var item1 = Item.Create(ItemId.Of("Item1"), "Item 1", ItemForm.Solid, false);
        var item2 = Item.Create(ItemId.Of("Item2"), "Item 2", ItemForm.Solid, false);

        Assert.NotEqual(item1, item2);
        Assert.NotEqual(item1.GetHashCode(), item2.GetHashCode());
    }
    
    [Fact]
    public void Items_with_same_ids_are_equal()
    {
        var item1 = Item.Create(ItemId.Of("Item1"), "Item 1", ItemForm.Solid, false);
        var item2 = Item.Create(ItemId.Of("Item1"), "Item 2", ItemForm.Solid, false);

        Assert.Equal(item1, item2);
        Assert.Equal(item1.GetHashCode(), item2.GetHashCode());
    }
    
    [Fact]
    public void Throws_if_name_is_empty()
    {
        Assert.Throws<ArgumentException>(() => Item.Create(ItemId.Of("Foo"), "", ItemForm.Solid, false));
    }
}