using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.Domain.Tests.GameObjects;

public class BuildingTests
{
    [Fact]
    public void Buildings_with_different_ids_are_not_equal()
    {
        var building1 = Building.Create(BuildingId.Of("Building1"), "Building 1", 0);
        var building2 = Building.Create(BuildingId.Of("Building2"), "Building 2", 0);

        Assert.NotEqual(building1, building2);
        Assert.NotEqual(building1.GetHashCode(), building2.GetHashCode());
    }
    
    [Fact]
    public void Buildings_with_same_ids_are_equal()
    {
        var building1 = Building.Create(BuildingId.Of("Building1"), "Building 1", 0);
        var building2 = Building.Create(BuildingId.Of("Building1"), "Building 2", 0);

        Assert.Equal(building1, building2);
        Assert.Equal(building1.GetHashCode(), building2.GetHashCode());
    }
    
    [Fact]
    public void Throws_if_name_is_empty()
    {
        Assert.Throws<ArgumentException>(() => Building.Create(BuildingId.Of("Foo"), "", 0));
    }

    [Fact] 
    public void Throws_if_somerSloopSlots_is_greater_than_4()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Building.Create(BuildingId.Of("Foo"), "Bar", 5));
    }
    
    [Fact] 
    public void Throws_if_somerSloopSlots_is_negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Building.Create(BuildingId.Of("Foo"), "Bar", -1));
    }
}