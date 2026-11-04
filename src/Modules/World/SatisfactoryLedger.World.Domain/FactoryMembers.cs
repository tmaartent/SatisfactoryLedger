namespace SatisfactoryLedger.World.Domain;

/// <summary>The machines and logistics that belong to one factory, found by <see cref="FactoryDefinition.TraceMembers"/>.</summary>
public sealed class FactoryMembers(IReadOnlySet<BuildableId> machines, IReadOnlySet<BuildableId> logistics)
{
    public IReadOnlySet<BuildableId> Machines { get; } = machines;

    public IReadOnlySet<BuildableId> Logistics { get; } = logistics;

    public bool Contains(BuildableId id) => Machines.Contains(id) || Logistics.Contains(id);
}
