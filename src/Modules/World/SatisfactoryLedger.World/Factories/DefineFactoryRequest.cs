namespace SatisfactoryLedger.World.Factories;

/// <param name="Machines">The factory's machines (BR-W2).</param>
internal sealed record DefineFactoryRequest(string? World, string? Name, string[]? Machines);
