using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.Domain.Tests.GameObjects;

public class RecipeIdTests
{
    [Fact]
    public void RecipeId_can_be_created()
    {
        RecipeId id = RecipeId.Of("Foo");
        
        Assert.Equal("Foo", id.Id);
    }
    
    [Fact]
    public void Throws_if_id_is_empty()
    {
        Assert.Throws<ArgumentException>(() => RecipeId.Of(""));
    }
    
    [Fact]
    public void Throws_if_id_is_whitespace()
    {
        Assert.Throws<ArgumentException>(() => RecipeId.Of("   "));
    }
}