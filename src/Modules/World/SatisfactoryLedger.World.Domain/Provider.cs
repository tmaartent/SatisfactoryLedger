namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// Brings items into the network from outside: a miner, an extractor, or a train, truck or drone station that unloads.
/// Never part of a factory (BR-W1).
/// </summary>
public sealed class Provider(BuildableId id) : Buildable(id);
