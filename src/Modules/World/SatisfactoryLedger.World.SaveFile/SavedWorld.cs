using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.SaveFile;

/// <summary>
/// What a save file says about the world, in our own terms (ADR-0013). Recipes, extractors and nodes are still
/// class names here: turning them into items and rates takes the Catalog, which the adapter doesn't know.
/// </summary>
public sealed record SavedWorld(
    SaveInfo Info,
    IReadOnlyList<SavedBuildable> Buildables,
    IReadOnlyList<Connection> Connections,
    IReadOnlyList<UnsupportedBuildables> Unsupported)
{
    /// <summary>What the dimensional depot holds, per item class name (BR-A7). Empty when the world has no depot.</summary>
    public IReadOnlyDictionary<string, decimal> DepotStock { get; init; } = new Dictionary<string, decimal>();

    /// <summary>
    /// Which pipes, junctions, pumps and buffers touch, without a direction (decided 2026-11-18): the model joins them into
    /// one network (BR-A6), and these say which way a pipe runs, to follow it piece by piece.
    /// </summary>
    public IReadOnlyList<(BuildableId A, BuildableId B)> PipeLinks { get; init; } = [];
}