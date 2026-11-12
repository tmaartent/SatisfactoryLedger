namespace SatisfactoryLedger.World.Factories;

internal sealed record DefineFactoryCommand(string World, string Name, IReadOnlyList<string> Machines);
