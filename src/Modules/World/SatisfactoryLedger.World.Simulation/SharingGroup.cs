using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>
/// Connections that share evenly at one node (BR-A4, BR-A5): a splitter's outputs, the inputs of a merger or of any
/// logistics fed by more than one connection, or the belts that bring a machine one of its inputs.
/// </summary>
/// <param name="Edges">The connections, by their position in the network.</param>
/// <param name="Item">For a machine's inputs, the item they share; null when they share everything they carry.</param>
internal sealed record SharingGroup(IReadOnlyList<int> Edges, ItemId? Item);
