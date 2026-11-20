namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>A factory the player defined, and whether all of its machines are still in the snapshot.</summary>
internal sealed record FactoryAnswer(string Id, string Name, bool Traceable, int Machines);
