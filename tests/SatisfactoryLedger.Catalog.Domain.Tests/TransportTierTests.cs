namespace SatisfactoryLedger.Catalog.Domain.Tests;

public class TransportTierTests
{
    [Fact]
    public void TransportTiers_with_different_ids_are_not_equal()
    {
        var transportTier1 = TransportTier.Create(
            TransportTierId.Of("IronPlates20"),
            "Foo",
            1200,
            TransportForm.Belt);
            
        
        var transportTier2 = TransportTier.Create(
            TransportTierId.Of("IronPlates21"),
            "Bar",
            1200,
            TransportForm.Belt);
        
        Assert.NotEqual(transportTier1, transportTier2);
        
        Assert.NotEqual(transportTier1.GetHashCode(), transportTier2.GetHashCode());
    }
    
    [Fact]
    public void TransportTiers_with_same_ids_are_equal()
    {
        var transportTier1 = TransportTier.Create(
            TransportTierId.Of("IronPlates20"),
            "Foo",
            1200,
            TransportForm.Belt);
        
        var transportTier2 = TransportTier.Create(
            TransportTierId.Of("IronPlates20"),
            "Bar",
            1200,
            TransportForm.Belt);
        
        Assert.Equal(transportTier1, transportTier2);
        
        Assert.Equal(transportTier1.GetHashCode(), transportTier2.GetHashCode());
    }
    
    [Fact]
    public void Throws_if_name_is_empty()
    {
        Assert.Throws<ArgumentException>(() => TransportTier.Create(
            TransportTierId.Of("IronPlates20"),
            "  ",
            1200,
            TransportForm.Belt));
    }
    
    [Fact]
    public void Throws_if_capacity_is_Negative()
    {
        Assert.Throws<ArgumentException>(() => TransportTier.Create(
            TransportTierId.Of("IronPlates20"),
            "Foo",
            -1,
            TransportForm.Belt));
    }
    
    [Fact]
    public void Throws_if_capacity_is_zero()
    {
        Assert.Throws<ArgumentException>(() => TransportTier.Create(
            TransportTierId.Of("IronPlates20"),
            "Foo",
            0,
            TransportForm.Belt));
    }
    
    [Fact]
    public void Throws_if_capacity_invalid()
    {
        Assert.Throws<ArgumentException>(() => TransportTier.Create(
            TransportTierId.Of("IronPlates20"),
            "Foo",
            133,
            TransportForm.Belt));
    }
    
    [Fact]
    public void Throws_if_capacity_exceeds_form_max()
    {
        Assert.Throws<ArgumentException>(() => TransportTier.Create(
            TransportTierId.Of("IronPlates20"),
            "Foo",
            666,
            TransportForm.Pipe));
    }
    
    
}