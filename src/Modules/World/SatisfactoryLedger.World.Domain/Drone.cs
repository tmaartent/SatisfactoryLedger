namespace SatisfactoryLedger.World.Domain;

/// <summary>A drone, between its home port and the port it's paired with (BR-A16).</summary>
/// <param name="Route">Null when it hasn't made a trip yet: what it burns isn't known.</param>
public sealed record Drone(BuildableId Home, BuildableId Paired, DroneRoute? Route);