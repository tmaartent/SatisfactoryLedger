namespace SatisfactoryLedger.World.Domain.Flow;

/// <summary>A belt, lift or pipe, or a container inside a line: passes items on, at most <paramref name="Capacity"/> per minute (BR-A3).</summary>
public sealed record FlowConduit(BuildableId Id, double Capacity, Carries Carries = Carries.Anything) : FlowNode(Id);