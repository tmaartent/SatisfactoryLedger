using SatisfactoryLedger.SharedKernel;

namespace SatisfactoryLedger.Catalog.Browsing;

/// <summary>A rate in both shapes (chapter 05, "Representation"): a decimal for display, the exact fraction for calculations.</summary>
internal sealed record RateResponse(decimal PerMinute, string Exact)
{
    public static RateResponse From(Rate rate) => new(rate.ToDecimal(), rate.ToExactString());
}