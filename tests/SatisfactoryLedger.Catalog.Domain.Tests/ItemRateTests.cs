using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.Catalog.Domain.Tests;

public class ItemRateTests
{
    [Fact]
    public void ItemRate_can_be_created()
    {
        ItemRate itemRate = ItemRate.Of(TestCatalog.Items.IronPlate.Id, Rate.PerMinute(20));
        
        Assert.Equal("Desc_IronPlate_C", itemRate.ItemId.Id);
        Assert.Equal(Rate.PerMinute(20), itemRate.Rate);
    }
}