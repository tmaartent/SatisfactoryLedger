namespace SatisfactoryLedger.Api.IntegrationTests.Catalog;

public sealed record CatalogIngredient(string ItemId, decimal AmountPerCycle, CatalogRate Rate);