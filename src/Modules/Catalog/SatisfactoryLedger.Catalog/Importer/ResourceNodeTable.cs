using SatisfactoryLedger.Catalog.Domain;

namespace SatisfactoryLedger.Catalog.Importer;

/// <summary>A node table, read: its game version and its nodes as Catalog entries.</summary>
internal sealed record ResourceNodeTable(string GameVersion, IReadOnlyList<ResourceNode> Nodes);