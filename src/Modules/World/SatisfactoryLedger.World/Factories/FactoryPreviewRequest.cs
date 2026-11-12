namespace SatisfactoryLedger.World.Factories;

/// <param name="Machines">The machines chosen so far: ids of machines or generators in the snapshot.</param>
internal sealed record FactoryPreviewRequest(string[]? Machines);
