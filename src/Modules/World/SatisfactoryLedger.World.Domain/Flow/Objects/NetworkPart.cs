namespace SatisfactoryLedger.World.Domain.Flow.Objects;

/// <summary>A connected part of a flow network, as a network of its own (BR-A10).</summary>
/// <param name="Edges">Per connection of <paramref name="Network"/>, in its order: its position in the whole network.</param>
public sealed record NetworkPart(FlowNetwork Network, IReadOnlyList<int> Edges);
