namespace SatisfactoryLedger.Api.IntegrationTests.Catalog;

public sealed record CatalogResourceNode(string Name, string Kind, string Resource, string Purity, CatalogPosition Position, bool InGame);