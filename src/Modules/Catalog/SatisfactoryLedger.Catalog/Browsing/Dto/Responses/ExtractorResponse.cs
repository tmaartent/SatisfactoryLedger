using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.Browsing.Dto.Responses;

internal sealed record ExtractorResponse(
    string Id,
    string Name,
    ItemForm Form,
    decimal AmountPerCycle,
    decimal CycleSeconds,
    RateResponse ExtractionRate,
    bool InGame)
{
    public static ExtractorResponse From(Extractor extractor) => new(
        extractor.Id.Id,
        extractor.Name,
        extractor.Form,
        extractor.AmountPerCycle,
        extractor.CycleSeconds,
        RateResponse.From(extractor.ExtractionRate),
        extractor.IsInGame);
}