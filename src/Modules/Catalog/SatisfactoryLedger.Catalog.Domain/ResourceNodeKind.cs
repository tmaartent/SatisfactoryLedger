namespace SatisfactoryLedger.Catalog.Domain;

/// <summary>A node a miner or oil extractor stands on, or a satellite node of a resource well.</summary>
public enum ResourceNodeKind
{
    ResourceNode,
    WellSatellite,
}