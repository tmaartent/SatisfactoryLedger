using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Factories;

/// <summary>A factory's stored machines found in one snapshot (BR-W11).</summary>
/// <param name="Ids">
/// Per stored machine, the id to trace in this snapshot: its own, or the id of the machine that replaced it. A missing
/// machine keeps its own id, which the snapshot doesn't have.
/// </param>
/// <param name="Missing">The stored machines with no match in this snapshot.</param>
internal sealed record MachineMatch(IReadOnlyList<BuildableId> Ids, IReadOnlyList<StoredMachine> Missing)
{
    /// <summary>How far a rebuilt machine may stand from where it stood, in centimetres: a metre (BR-W11).</summary>
    public const double Within = 100;

    /// <summary>
    /// Finds each machine by its id, or else the machine of its class that stands closest to where it stood, within a
    /// metre. A machine in the snapshot replaces one stored machine at most.
    /// </summary>
    public static MachineMatch In(SnapshotDocument snapshot, IEnumerable<StoredMachine> machines)
    {
        Dictionary<string, BuildableDocument> snapshotMachines = snapshot.Buildables
            .Where(buildable => buildable.Role == BuildableDocumentRole.Machine)
            .ToDictionary(buildable => buildable.Id, StringComparer.Ordinal);
        List<StoredMachine> stored = machines.ToList();
        HashSet<string> taken = [.. stored.Select(machine => machine.Id).Where(snapshotMachines.ContainsKey)];

        List<BuildableId> ids = [];
        List<StoredMachine> missing = [];
        foreach (StoredMachine machine in stored)
        {
            string? id = snapshotMachines.ContainsKey(machine.Id) ? machine.Id : Replacement(machine);
            if (id is null)
            {
                missing.Add(machine);
            }

            ids.Add(BuildableId.Of(id ?? machine.Id));
        }

        return new MachineMatch(ids, missing);

        string? Replacement(StoredMachine machine)
        {
            if (machine is not { X: { } x, Y: { } y, Z: { } z })
            {
                return null;
            }

            string? closest = snapshotMachines.Values
                .Where(candidate => candidate.ClassName == machine.ClassName && candidate.Position is not null && !taken.Contains(candidate.Id))
                .Select(candidate => (candidate.Id, Distance: Distance(candidate.Position!, x, y, z)))
                .Where(candidate => candidate.Distance <= Within)
                .OrderBy(candidate => candidate.Distance)
                .Select(candidate => candidate.Id)
                .FirstOrDefault();
            if (closest is not null)
            {
                taken.Add(closest);
            }

            return closest;
        }
    }

    private static double Distance(PositionDocument position, double x, double y, double z) =>
        Math.Sqrt(Math.Pow(position.X - x, 2) + Math.Pow(position.Y - y, 2) + Math.Pow(position.Z - z, 2));
}
