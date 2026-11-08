using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.SaveFile;

/// <summary>The save's links as connections with a direction, and the pipe networks they made into hubs.</summary>
internal sealed record Wiring(IReadOnlyList<Connection> Connections, IReadOnlyList<PipeNetwork> PipeNetworks);