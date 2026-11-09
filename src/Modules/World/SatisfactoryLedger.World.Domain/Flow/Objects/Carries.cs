namespace SatisfactoryLedger.World.Domain.Flow.Objects;

/// <summary>What a node can move: belts, splitters, mergers and containers move solids; pipes, junctions and fluid buffers fluids.</summary>
public enum Carries
{
    /// <summary>Not limited by the node itself: a machine, a sink, a station. Its connection's other end decides.</summary>
    Anything,
    Solids,
    Fluids,
}