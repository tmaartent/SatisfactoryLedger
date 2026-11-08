namespace SatisfactoryLedger.World.Domain;

/// <param name="Belt">The belt or lift the player could pick as a factory's output belt.</param>
/// <param name="Items">What the machines feeding it produce.</param>
/// <param name="Into">What it ends in: a storage container or consumer, or nothing.</param>
public sealed record OutputBeltCandidate(BuildableId Belt, IReadOnlySet<ItemId> Items, IReadOnlyList<BuildableId> Into);