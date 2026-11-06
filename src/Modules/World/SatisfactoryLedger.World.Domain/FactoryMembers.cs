namespace SatisfactoryLedger.World.Domain;

/// <summary>The machines and logistics that belong to one factory, found by <see cref="FactoryDefinition.TraceMembers"/>.</summary>
/// <param name="suppliers">The providers and sources of unknown rate that feed it: never members (BR-W1), but its supply (BR-A14).</param>
/// <param name="producers">Its machines whose output reaches the output belt through logistics only: what it makes.</param>
public sealed class FactoryMembers(
    IReadOnlySet<BuildableId> machines,
    IReadOnlySet<BuildableId> logistics,
    IReadOnlySet<BuildableId>? suppliers = null,
    IReadOnlySet<BuildableId>? producers = null)
{
    public IReadOnlySet<BuildableId> Machines { get; } = machines;

    public IReadOnlySet<BuildableId> Logistics { get; } = logistics;

    /// <summary>The providers, stations and unfed containers on its input lines. A supplier can feed several factories.</summary>
    public IReadOnlySet<BuildableId> Suppliers { get; } = suppliers ?? new HashSet<BuildableId>();

    /// <summary>The machines that make what leaves on the output belt.</summary>
    public IReadOnlySet<BuildableId> Producers { get; } = producers ?? new HashSet<BuildableId>();

    public bool Contains(BuildableId id) => Machines.Contains(id) || Logistics.Contains(id);
}
