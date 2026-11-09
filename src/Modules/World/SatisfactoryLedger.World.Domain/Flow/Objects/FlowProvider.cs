namespace SatisfactoryLedger.World.Domain.Flow.Objects;

/// <param name="Offer">What it can deliver per minute (BR-A2).</param>
public sealed record FlowProvider(BuildableId Id, ItemId Item, double Offer) : FlowNode(Id);