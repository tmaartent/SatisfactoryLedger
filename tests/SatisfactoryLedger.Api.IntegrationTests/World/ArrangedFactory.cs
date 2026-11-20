namespace SatisfactoryLedger.Api.IntegrationTests.World;

public sealed record ArrangedFactory(Guid FactoryId, string? Name, WorldRectangle Now, WorldRectangle? Proposed);
