namespace SatisfactoryLedger.World.Factories;

internal sealed record DefineFactoryCommand(string World, string Name, string OutputBelt, IReadOnlyList<string> InputItems);