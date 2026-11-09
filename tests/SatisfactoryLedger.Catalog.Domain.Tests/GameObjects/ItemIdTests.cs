using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.Domain.Tests.GameObjects;

public class ItemIdTests
{
    [Fact]
    public void ItemId_can_be_created()
    {
        ItemId id = ItemId.Of(("id"));
        
        Assert.Equal("id", id.Id);
    }
    
    [Fact]
    public void Throws_if_id_is_empty()
    {
        Assert.Throws<ArgumentException>(() => ItemId.Of(""));
    }
    
    [Fact]
    public void Throws_if_id_is_whitespace()
    {
        Assert.Throws<ArgumentException>(() => ItemId.Of("   "));
    }
}