namespace SatisfactoryLedger.Api.IntegrationTests.World;

public sealed record ArrangedMachine(Guid FactoryId, string Id, string ClassName, Position Position, double Yaw);
