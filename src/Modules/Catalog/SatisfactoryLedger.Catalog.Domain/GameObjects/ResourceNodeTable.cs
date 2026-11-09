namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

/// <summary>A node table, read: its game version and its nodes as Catalog entries.</summary>
public sealed record ResourceNodeTable(string GameVersion, IReadOnlyList<ResourceNode> Nodes);