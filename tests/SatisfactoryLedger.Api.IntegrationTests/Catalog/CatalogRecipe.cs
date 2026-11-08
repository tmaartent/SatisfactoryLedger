namespace SatisfactoryLedger.Api.IntegrationTests.Catalog;

public sealed record CatalogRecipe(
    string Id,
    string Name,
    string BuildingId,
    decimal CycleSeconds,
    bool IsAlternate,
    bool InGame,
    CatalogIngredient[] Inputs,
    CatalogIngredient[] Outputs);