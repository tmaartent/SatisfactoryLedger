namespace SatisfactoryLedger.Api.IntegrationTests.Catalog;

public sealed record CatalogItem(string Id, string Name, string Form, bool IsRawResource, bool InGame);