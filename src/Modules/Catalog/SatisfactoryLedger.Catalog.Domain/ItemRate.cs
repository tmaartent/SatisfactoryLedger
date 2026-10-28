using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.Catalog.Domain;

public record ItemRate(ItemId ItemId, Rate Rate)
{
    public static ItemRate Of(ItemId itemId, Rate rate)
    {
        return new ItemRate(itemId, rate);
    }
}