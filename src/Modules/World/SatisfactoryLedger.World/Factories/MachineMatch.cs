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
    /// Finds every factory's machines in one pass: each by its id, or else the machine of its class that stands closest
    /// to where it stood, within a metre. A machine in the snapshot replaces one stored machine at most, across all the
    /// factories: the oldest factory's first (<see cref="StoredFactory.OldestFirst"/>), and never a machine one of them
    /// has by its own id.
    /// </summary>
    /// <returns>Per factory, in the order given, its machines as matched.</returns>
    public static IReadOnlyList<MachineMatch> In(SnapshotDocument snapshot, IReadOnlyList<StoredFactory> factories)
    {
        Dictionary<string, BuildableDocument> snapshotMachines = snapshot.Buildables
            .Where(buildable => buildable.IsMachineOrGenerator)
            .ToDictionary(buildable => buildable.Id, StringComparer.Ordinal);
        HashSet<string> taken = [.. factories.SelectMany(factory => factory.Machines).Select(machine => machine.Id).Where(snapshotMachines.ContainsKey)];

        // A stored machine two factories have (from before BR-W5) is one machine: it gets one replacement, or none.
        Dictionary<string, string?> replacements = new(StringComparer.Ordinal);
        foreach (StoredMachine machine in factories.Order(StoredFactory.OldestFirst).SelectMany(factory => factory.Machines))
        {
            if (!snapshotMachines.ContainsKey(machine.Id) && !replacements.ContainsKey(machine.Id))
            {
                replacements[machine.Id] = Replacement(machine);
            }
        }

        return [.. factories.Select(factory => Matched(factory.Machines))];

        MachineMatch Matched(List<StoredMachine> machines)
        {
            List<BuildableId> ids = [];
            List<StoredMachine> missing = [];
            foreach (StoredMachine machine in machines)
            {
                string? id = snapshotMachines.ContainsKey(machine.Id) ? machine.Id : replacements[machine.Id];
                if (id is null)
                {
                    missing.Add(machine);
                }

                ids.Add(BuildableId.Of(id ?? machine.Id));
            }

            return new MachineMatch(ids, missing);
        }

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
