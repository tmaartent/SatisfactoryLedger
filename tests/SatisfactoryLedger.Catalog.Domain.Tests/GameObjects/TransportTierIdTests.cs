using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.Domain.Tests.GameObjects;

public class TransportTierIdTests
{
    [Fact]
    public void TransportTierId_can_be_created()
    {
        TransportTierId id = TransportTierId.Of("Foo");
        
        Assert.Equal("Foo", id.Id);
    }
    
    [Fact]
    public void Throws_if_id_is_empty()
    {
        Assert.Throws<ArgumentException>(() => TransportTierId.Of(""));
    }
    
    [Fact]
    public void Throws_if_id_is_whitespace()
    {
        Assert.Throws<ArgumentException>(() => TransportTierId.Of("   "));
    }
}