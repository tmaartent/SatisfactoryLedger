namespace SatisfactoryLedger.Api.IntegrationTests.World;

public sealed record OutputBelt(string Belt, string ClassName, string[] Items, Reference[] Into, Position? Position, Dictionary<string, int> Machines);