using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider.Usmap;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;

namespace SatisfactoryLedger.MapExtractor;

/// <summary>
/// Reads the resource nodes from a game install with CUE4Parse (spike log 06). The only class that uses CUE4Parse.
/// All of the game's nodes, resource well satellites included, are in one package, the main level.
/// </summary>
internal sealed class GameMap(string gameFolder)
{
    private const string Level = "FactoryGame/Content/FactoryGame/Map/GameLevel01/Persistent_Level.umap";
    /// <summary>The node classes read: what miners and oil extractors stand on, and resource well satellites.</summary>
    private static readonly Dictionary<string, NodeKind> NodeClasses = new(StringComparer.Ordinal)
    {
        ["BP_ResourceNode_C"] = NodeKind.ResourceNode,
        ["BP_FrackingSatellite_C"] = NodeKind.WellSatellite,
    };

    private string Paks => Path.Combine(gameFolder, "FactoryGame", "Content", "Paks");

    private string TypeMap => Path.Combine(gameFolder, "CommunityResources", "FactoryGame.usmap");

    /// <summary>Why this folder can't be read, or null when it looks like a game install.</summary>
    public string? Problem() =>
        !Directory.Exists(Paks) ? $"No game files in {Paks}. Give the game's install folder."
        : !File.Exists(TypeMap) ? $"No type map at {TypeMap}."
        : null;

    public IReadOnlyList<MapNode> ResourceNodes()
    {
        UsmapPatcher.Result typeMap = UsmapPatcher.Patch(File.ReadAllBytes(TypeMap));
        string patchedTypeMap = Path.Combine(Path.GetTempPath(), $"FactoryGame.{Guid.NewGuid():N}.usmap");
        File.WriteAllBytes(patchedTypeMap, typeMap.Usmap);

        try
        {
            using var provider = new DefaultFileProvider(Paks, SearchOption.TopDirectoryOnly,
                new VersionContainer(EGame.GAME_UE5_6), StringComparer.OrdinalIgnoreCase);
            provider.MappingsContainer = new FileUsmapTypeMappingsProvider(patchedTypeMap, StringComparer.OrdinalIgnoreCase);
            provider.Initialize();
            // The game's files aren't encrypted: an all-zero key unlocks them.
            provider.SubmitKey(new FGuid(), new FAesKey(new byte[32]));

            IPackage level = provider.LoadPackage(Level);
            return
            [
                .. level.GetExports()
                    .Where(export => NodeClasses.ContainsKey(export.ExportType))
                    .Select(export => ToMapNode(export, NodeClasses[export.ExportType]))
            ];
        }
        finally
        {
            File.Delete(patchedTypeMap);
        }
    }

    private static MapNode ToMapNode(UObject node, NodeKind kind) => new(
        node.Name,
        kind,
        node.GetOrDefault<FPackageIndex>("mResourceClass")?.ToString(),
        node.Properties.FirstOrDefault(property => property.Name.Text == "mPurity")?.Tag?.GenericValue?.ToString(),
        PositionOf(node));

    /// <summary>A node's position is its root component's location (a box around the node).</summary>
    private static NodePosition? PositionOf(UObject node)
    {
        UObject? root = node.GetOrDefault<FPackageIndex>("RootComponent")?.Load<UObject>();
        if (root is null || !root.TryGetValue(out FVector location, "RelativeLocation"))
        {
            return null;
        }

        return new NodePosition(location.X, location.Y, location.Z);
    }
}
