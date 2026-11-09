namespace SatisfactoryLedger.World.Domain;

/// <summary>What belongs to one factory in one world, found by <see cref="FactoryDefinition.TraceMembers"/>.</summary>
/// <param name="machines">Its machines that are in this world.</param>
/// <param name="logistics">The logistics it owns (BR-W3).</param>
/// <param name="outputs">Where it hands over to the world (BR-W4).</param>
/// <param name="inputs">Where the world hands over to it (BR-W4).</param>
/// <param name="suppliers">The providers and sources of unknown rate that feed it: never members (BR-W1), but its supply (BR-A14).</param>
/// <param name="producers">Its machines whose output leaves the factory: what it makes.</param>
public sealed class FactoryMembers(
    IReadOnlySet<BuildableId> machines,
    IReadOnlySet<BuildableId> logistics,
    IReadOnlyList<FactoryConnection> outputs,
    IReadOnlyList<FactoryConnection> inputs,
    IReadOnlySet<BuildableId> suppliers,
    IReadOnlySet<BuildableId> producers)
{
    public IReadOnlySet<BuildableId> Machines { get; } = machines;

    public IReadOnlySet<BuildableId> Logistics { get; } = logistics;

    public IReadOnlyList<FactoryConnection> Outputs { get; } = outputs;

    public IReadOnlyList<FactoryConnection> Inputs { get; } = inputs;

    /// <summary>The providers, stations and unfed containers its inputs reach. A supplier can feed several factories.</summary>
    public IReadOnlySet<BuildableId> Suppliers { get; } = suppliers;

    /// <summary>The machines that make what leaves on its outputs.</summary>
    public IReadOnlySet<BuildableId> Producers { get; } = producers;

    public bool Contains(BuildableId id) => Machines.Contains(id) || Logistics.Contains(id);
}
