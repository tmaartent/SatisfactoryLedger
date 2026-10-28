namespace SatisfactoryLedger.Catalog.Domain.Tests;

public class BuildingIdTests
{
    [Fact]
    public void BuildingId_can_be_created()
    {
        BuildingId id = BuildingId.Of("id");
        
        Assert.Equal("id", id.Id);
    }

    [Fact]
    public void Throws_if_id_is_empty()
    {
        Assert.Throws<ArgumentException>(() => BuildingId.Of(""));
    }
    
    [Fact]
    public void Throws_if_id_is_whitespace()
    {
        Assert.Throws<ArgumentException>(() => BuildingId.Of("   "));
    }
}