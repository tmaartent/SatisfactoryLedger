namespace SatisfactoryLedger.Api.IntegrationTests.Catalog;

// The response shapes as a client sees them. Kept separate from the API's own types on purpose:
// renaming a field in the API should break these tests, because it breaks the contract.
public sealed record ImportCounts(int Added, int Updated, int NoLongerInGame);