using SatisfactoryLedger.World.Domain;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Factories;

/// <summary>
/// A machine belongs to one factory at most (BR-W5). The rule spans every factory of a world, so it's checked here
/// when a factory is defined, beside the unique name, rather than inside one factory's definition.
/// </summary>
internal static class ClaimedMachines
{
    /// <summary>
    /// The chosen machines that one of the factories already has in this snapshot: by its id, or as the machine that
    /// replaced one of its own (BR-W11). In the order chosen.
    /// </summary>
    public static IReadOnlyList<TakenMachine> Among(IEnumerable<string> chosen, SnapshotDocument snapshot, IEnumerable<StoredFactory> factories)
    {
        Dictionary<string, string> owners = new(StringComparer.Ordinal);
        foreach (StoredFactory factory in factories)
        {
            MachineMatch match = MachineMatch.In(snapshot, factory.Machines);
            foreach (BuildableId id in match.Ids)
            {
                owners.TryAdd(id.Id, factory.Name);
            }
        }

        return [.. chosen.Where(owners.ContainsKey).Select(id => new TakenMachine(id, owners[id]))];
    }
}
