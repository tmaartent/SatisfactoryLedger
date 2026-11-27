using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.Factories;

/// <summary>
/// A factory definition as stored: it belongs to a world (a session name), not to a snapshot, and is traced in every
/// snapshot of that world (BR-W11). The domain's <see cref="FactoryDefinition"/> is made from it per snapshot.
/// </summary>
internal sealed class StoredFactory
{
    /// <summary>
    /// The oldest factory first: ids are <c>Guid</c> v7, so the lowest id is the oldest. Of factories that both have a
    /// machine, from before BR-W5, or that both match a rebuilt one, the oldest owns it (BR-W5, BR-W11).
    /// </summary>
    public static IComparer<StoredFactory> OldestFirst { get; } = Comparer<StoredFactory>.Create((one, other) => one.Id.CompareTo(other.Id));

    public required Guid Id { get; init; }

    /// <summary>The session name of the world it belongs to.</summary>
    public required string World { get; init; }

    public required string Name { get; set; }

    /// <summary>Its machines (BR-W2).</summary>
    public List<StoredMachine> Machines { get; set; } = [];

    /// <summary>The definition to trace in a snapshot, with its machines as matched there; null when it has none.</summary>
    public FactoryDefinition? ToDefinition(MachineMatch match) => Machines.Count == 0 ? null : new(match.Ids);
}
