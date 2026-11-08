namespace SatisfactoryLedger.World.Snapshots;

/// <summary>The Catalog copy of a snapshot, by id, to look up while building the world.</summary>
internal sealed record CopiedCatalog(
    IReadOnlyDictionary<string, RecipeDocument> Recipes,
    IReadOnlyDictionary<string, ExtractorDocument> Extractors,
    IReadOnlyDictionary<string, TransportTierDocument> TransportTiers,
    IReadOnlyDictionary<string, ResourceNodeDocument> ResourceNodes,
    IReadOnlyDictionary<string, GeneratorDocument> Generators);