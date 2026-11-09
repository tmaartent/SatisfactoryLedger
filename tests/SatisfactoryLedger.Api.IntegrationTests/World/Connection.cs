namespace SatisfactoryLedger.Api.IntegrationTests.World;

public sealed record Connection(Reference Inside, Reference? Outside, string[] Items);