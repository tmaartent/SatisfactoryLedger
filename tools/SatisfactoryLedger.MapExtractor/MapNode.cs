namespace SatisfactoryLedger.MapExtractor;

/// <summary>What the game's map says about one resource node, as read: values the map didn't store are null.</summary>
/// <param name="ResourceClass">The resource as the map stores it: <c>BlueprintGeneratedClass'/Game/…/Desc_OreIron.Desc_OreIron_C'</c>.</param>
/// <param name="Purity">The stored purity (<c>RP_Inpure</c>, <c>RP_Pure</c>), or null when the default (normal) applies.</param>
internal sealed record MapNode(string Name, NodeKind Kind, string? ResourceClass, string? Purity, NodePosition? Position);