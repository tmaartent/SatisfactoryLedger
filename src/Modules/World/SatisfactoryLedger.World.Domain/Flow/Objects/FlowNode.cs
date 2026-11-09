namespace SatisfactoryLedger.World.Domain.Flow.Objects;

/// <summary>A node of the flow network: a buildable with what it asks, offers or carries per minute (BR-A1 to BR-A9).</summary>
public abstract record FlowNode(BuildableId Id);