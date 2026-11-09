using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Snapshots;

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

    /// <summary>Its machines (BR-W2). Empty only for a factory defined by its output belt that couldn't be converted yet.</summary>
    public List<StoredMachine> Machines { get; set; } = [];

    /// <summary>The output belt of a factory defined the old way, until it's converted to its machines.</summary>
    public string? OutputBelt { get; set; }

    /// <summary>The input items of a factory defined the old way, until it's converted to its machines.</summary>
    public List<string>? InputItems { get; set; }

    /// <summary>The definition to trace in a snapshot, with its machines as matched there; null until it's converted.</summary>
    public FactoryDefinition? ToDefinition(MachineMatch match) => Machines.Count == 0 ? null : new(match.Ids);

    /// <summary>
    /// Converts a factory defined by its output belt and input items into its machines: traced once, in its world's
    /// latest snapshot (BR-W2). False when that trace finds no machines; it then stays as it was.
    /// </summary>
    public bool ConvertIn(SnapshotDocument latest)
    {
        if (OutputBelt is null)
        {
            return false;
        }

        WorldGraph world = latest.ToWorldGraph();
        var trace = new OutputBeltTrace(BuildableId.Of(OutputBelt), (InputItems ?? []).Select(ItemId.Of));
        if (!trace.IsTraceableIn(world))
        {
            return false;
        }

        IReadOnlySet<BuildableId> machines = trace.Machines(world);
        if (machines.Count == 0)
        {
            return false;
        }

        Dictionary<string, BuildableDocument> buildables = latest.Buildables.ToDictionary(buildable => buildable.Id, StringComparer.Ordinal);
        Machines = [.. machines.Select(id => StoredMachine.From(buildables[id.Id])).OrderBy(machine => machine.Id, StringComparer.Ordinal)];
        OutputBelt = null;
        InputItems = null;
        return true;
    }
}
