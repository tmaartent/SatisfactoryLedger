using SatisfactoryLedger.Catalog.Domain;
using SatisfactoryLedger.Catalog.Domain.GameObjects;

namespace SatisfactoryLedger.Catalog.GameData.NodeTable;

/// <summary>A node table, read: its game version and its nodes as Catalog entries.</summary>
internal sealed record ResourceNodeTable(string GameVersion, IReadOnlyList<ResourceNode> Nodes);