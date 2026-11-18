using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.SaveFile;

/// <summary>The save's links as connections with a direction, and the pipe networks they made into hubs.</summary>
/// <param name="PipeLinks">Which pipes, junctions, pumps and buffers touch, without a direction: the network's insides,
/// to follow a pipe piece by piece (decided 2026-11-18).</param>
internal sealed record Wiring(
    IReadOnlyList<Connection> Connections, IReadOnlyList<PipeNetwork> PipeNetworks, IReadOnlyList<(BuildableId A, BuildableId B)> PipeLinks);