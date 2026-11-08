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
    IReadOnlyList<UnsupportedBuildables> Unsupported);