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
    /// The chosen machines that another factory already has in this snapshot: by its id, or as the machine that
    /// replaced one of its own (BR-W11). In the order chosen. The factories are the world's, the claimant's stored
    /// version among them when it has one, so a rebuilt machine is matched as on every other page; then the claimant's
    /// own are left out of the claims.
    /// </summary>
    public static IReadOnlyList<TakenMachine> Among(IEnumerable<string> chosen, Guid claimant, SnapshotDocument snapshot, IEnumerable<StoredFactory> factories)
    {
        var inSnapshot = SnapshotFactories.Of(snapshot, factories);
        List<TakenMachine> taken = [];
        foreach (string id in chosen)
        {
            if (inSnapshot.Owners.TryGetValue(BuildableId.Of(id), out Guid owner) && owner != claimant)
            {
                taken.Add(new TakenMachine(id, inSnapshot.Find(owner)!.Factory.Name));
            }
        }

        return taken;
    }
}
