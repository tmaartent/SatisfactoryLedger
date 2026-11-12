namespace SatisfactoryLedger.World.Factories;

internal sealed record EditFactoryCommand(Guid Id, string Name, IReadOnlyList<string> Machines);
