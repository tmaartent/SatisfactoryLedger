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
        var inSnapshot = SnapshotFactories.Of(snapshot, factories);
        return [.. chosen
            .Where(id => inSnapshot.Owners.ContainsKey(BuildableId.Of(id)))
            .Select(id => new TakenMachine(id, inSnapshot.Find(inSnapshot.Owners[BuildableId.Of(id)])!.Factory.Name))];
    }
}
