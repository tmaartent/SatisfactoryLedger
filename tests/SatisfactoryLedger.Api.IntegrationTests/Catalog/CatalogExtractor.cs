namespace SatisfactoryLedger.Api.IntegrationTests.Catalog;

public sealed record CatalogExtractor(
    string Id, string Name, string Form, decimal AmountPerCycle, decimal CycleSeconds, CatalogRate ExtractionRate, bool InGame);