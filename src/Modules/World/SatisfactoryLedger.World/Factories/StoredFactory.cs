using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.Factories;

/// <summary>
/// A factory definition as stored: it belongs to a world (a session name), not to a snapshot, and is traced in every
/// snapshot of that world (BR-W11). The domain's <see cref="FactoryDefinition"/> is made from it per snapshot.
/// </summary>
internal sealed class StoredFactory
{
    public required Guid Id { get; init; }

    /// <summary>The session name of the world it belongs to.</summary>
    public required string World { get; init; }

    public required string Name { get; init; }

    /// <summary>Its machines (BR-W2).</summary>
    public List<StoredMachine> Machines { get; set; } = [];

    /// <summary>The definition to trace in a snapshot, with its machines as matched there; null when it has none.</summary>
    public FactoryDefinition? ToDefinition(MachineMatch match) => Machines.Count == 0 ? null : new(match.Ids);
}
