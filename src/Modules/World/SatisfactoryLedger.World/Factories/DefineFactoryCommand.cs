namespace SatisfactoryLedger.World.Factories;

/// <param name="OutputBelt">When given, the machines are traced from it instead (BR-W12).</param>
internal sealed record DefineFactoryCommand(
    string World, string Name, IReadOnlyList<string> Machines, string? OutputBelt, IReadOnlyList<string> InputItems);
