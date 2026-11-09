namespace SatisfactoryLedger.World.Domain;

/// <summary>
/// Where a factory hands over to the world, or the world to it (BR-W4): an output or an input.
/// </summary>
/// <param name="Inside">The factory's machine or logistics on this side of the handover.</param>
/// <param name="Outside">
/// What lies across it: another factory's machine, shared logistics, a provider or a consumer. Null when the line ends
/// inside the factory: a storage container at its end (an output) or one nothing feeds (an input).
/// </param>
/// <param name="Items">
/// For an output, what the factory's machines that reach it make; for an input, what the factory's machines it reaches
/// ask for and don't get from inside the factory. Ordered by item id.
/// </param>
public sealed record FactoryConnection(BuildableId Inside, BuildableId? Outside, IReadOnlyList<ItemId> Items);
