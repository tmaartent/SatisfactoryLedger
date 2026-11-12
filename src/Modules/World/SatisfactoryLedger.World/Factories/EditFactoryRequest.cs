namespace SatisfactoryLedger.World.Factories;

/// <param name="Name">Its name, the same or a new one (BR-W11).</param>
/// <param name="Machines">All its machines after the edit (BR-W2).</param>
internal sealed record EditFactoryRequest(string? Name, string[]? Machines);
