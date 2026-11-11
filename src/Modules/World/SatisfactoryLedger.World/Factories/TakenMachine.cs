namespace SatisfactoryLedger.World.Factories;

/// <summary>A chosen machine another factory of the world already has (BR-W5).</summary>
/// <param name="Id">The machine's id in the snapshot it was chosen in.</param>
/// <param name="Factory">The name of the factory that has it.</param>
internal sealed record TakenMachine(string Id, string Factory);
