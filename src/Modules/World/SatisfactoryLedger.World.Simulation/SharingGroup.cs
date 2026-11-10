using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.Simulation;

/// <summary>
/// Connections that share evenly at one node (BR-A4, BR-A5): the outputs of a splitter (per tier), the inputs of a
/// merger (per priority) or of any logistics fed by more than one connection, or the belts that bring a machine one of
/// its inputs.
/// </summary>
/// <param name="Node">The node they share at, by its position.</param>
/// <param name="Inputs">
/// True for connections into <paramref name="Node"/>: a share of them is capped when what feeds it can't offer more.
/// False for a splitter's outputs: capped when what lies beyond can't take more.
/// </param>
/// <param name="Edges">The connections, by their position in the network.</param>
/// <param name="Items">The items they share; null when they share everything they carry.</param>
/// <param name="Tier">
/// 0 for a group served first. A higher tier gets something only when every share of the groups in
/// <paramref name="Above"/> is capped: an overflow output, a lower priority, an uploader or a station's fuel (BR-A4, A5, A8).
/// </param>
/// <param name="Above">The groups, by their position, that are served before this one.</param>
internal sealed record SharingGroup(
    int Node, bool Inputs, IReadOnlyList<int> Edges, IReadOnlySet<ItemId>? Items, int Tier = 0, IReadOnlyList<int>? Above = null);
