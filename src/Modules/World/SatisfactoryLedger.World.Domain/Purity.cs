namespace SatisfactoryLedger.World.Domain;

/// <summary>How rich a resource node is (BR-W9). A water extractor counts as normal.</summary>
public enum Purity
{
    Impure,
    Normal,
    Pure,
}