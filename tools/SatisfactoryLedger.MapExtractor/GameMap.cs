using System.Runtime.InteropServices;

using AssetRipper.TextureDecoder.Bc;
using AssetRipper.TextureDecoder.Rgb.Formats;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider.Usmap;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using SkiaSharp;

namespace SatisfactoryLedger.MapExtractor;

/// <summary>
/// Reads the resource nodes and the map picture from a game install with CUE4Parse (spike logs 06 and 53). The only
/// class that uses CUE4Parse. All of the game's nodes, resource well satellites included, are in one package, the main
/// level; so is the actor that captured the map picture.
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

    /// <summary>The map picture's textures: the game cuts it in a grid, <c>Map_column-row</c>.</summary>
    private const string MapTextures = "FactoryGame/Content/FactoryGame/Interface/UI/Assets/MapTest/SlicedMap/";

    private const string CaptureActor = "BP_MinimapCaptureActor_C";

    /// <summary>JPEG quality for the map art: small enough to store and serve, sharp enough to read the terrain.</summary>
    private const int JpegQuality = 85;

    /// <summary>The icons' size, in pixels square (asked 2026-11-23): the game's own for many, sharp at any size shown.</summary>
    private const int IconSize = 256;

    private string Paks => Path.Combine(gameFolder, "FactoryGame", "Content", "Paks");

    private string TypeMap => Path.Combine(gameFolder, "CommunityResources", "FactoryGame.usmap");

    /// <summary>Why this folder can't be read, or null when it looks like a game install.</summary>
    public string? Problem() =>
        !Directory.Exists(Paks) ? $"No game files in {Paks}. Give the game's install folder."
        : !File.Exists(TypeMap) ? $"No type map at {TypeMap}."
        : null;

    public IReadOnlyList<MapNode> ResourceNodes() => Read(provider =>
    {
        IPackage level = provider.LoadPackage(Level);
        return (IReadOnlyList<MapNode>)
        [
            .. level.GetExports()
                .Where(export => NodeClasses.ContainsKey(export.ExportType))
                .Select(export => ToMapNode(export, NodeClasses[export.ExportType]))
        ];
    });

    /// <summary>
    /// The map picture (ADR-0020): where the capture actor stands and how wide a square its orthographic camera saw,
    /// and each texture decoded and encoded as JPEG.
    /// </summary>
    public MapCapture MapCapture() => Read(provider =>
    {
        UObject actor = provider.LoadPackage(Level).GetExports().FirstOrDefault(export => export.ExportType == CaptureActor)
            ?? throw new InvalidDataException($"The level has no {CaptureActor}");
        UObject? root = actor.GetOrDefault<FPackageIndex>("RootComponent")?.Load<UObject>();
        UObject? camera = actor.GetOrDefault<FPackageIndex>("CaptureComponent2D")?.Load<UObject>();
        if (root is null || !root.TryGetValue(out FVector centre, "RelativeLocation")
            || camera is null || !camera.TryGetValue(out float width, "OrthoWidth"))
        {
            throw new InvalidDataException("The map's capture actor has no place or width");
        }

        List<MapImage> images = [];
        foreach (string path in provider.Files.Keys.Where(IsMapTexture).Order(StringComparer.Ordinal))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            UTexture2D texture = provider.LoadPackageObject<UTexture2D>($"{path[..^".uasset".Length]}.{name}");
            images.Add(new MapImage(name, Jpeg(name, texture)));
        }

        return new MapCapture(centre.X, centre.Y, width, images);
    });

    /// <summary>A map texture as JPEG: its largest mip, decoded from DXT1, the only format the game's map comes in.</summary>
    private static byte[] Jpeg(string name, UTexture2D texture)
    {
        FTexture2DMipMap? mip = texture.GetFirstMip();
        byte[]? data = mip?.BulkData?.Data;
        if (texture.Format != EPixelFormat.PF_DXT1 || mip is null || data is null)
        {
            throw new InvalidDataException($"The map texture {name} isn't a DXT1 image ({texture.Format})");
        }

        Bc1.Decompress<ColorBGRA<byte>, byte>(data, mip.SizeX, mip.SizeY, out byte[] pixels);
        using var bitmap = new SKBitmap(new SKImageInfo(mip.SizeX, mip.SizeY, SKColorType.Bgra8888, SKAlphaType.Opaque));
        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        using SKData jpeg = bitmap.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return jpeg.ToArray();
    }

    /// <summary>
    /// The game's icons (ADR-0032): each texture the Docs file names, by descriptor, as a PNG at most
    /// <see cref="IconSize"/> pixels square.
    /// </summary>
    public IReadOnlyList<GameIcon> Icons(IReadOnlyDictionary<string, string> textures) => Read(provider =>
    {
        List<GameIcon> icons = [];
        foreach ((string className, string path) in textures)
        {
            UTexture2D texture = provider.LoadPackageObject<UTexture2D>(path);
            icons.Add(new GameIcon(className, Png(className, texture)));
        }

        return (IReadOnlyList<GameIcon>)icons;
    });

    /// <summary>
    /// An icon as PNG: its largest mip, decoded from DXT5 or plain BGRA, the formats the game's icons come in, and
    /// scaled down to <see cref="IconSize"/> when it's larger (most are 512).
    /// </summary>
    private static byte[] Png(string className, UTexture2D texture)
    {
        FTexture2DMipMap? mip = texture.GetFirstMip();
        byte[]? data = mip?.BulkData?.Data;
        if (mip is null || data is null)
        {
            throw new InvalidDataException($"The icon of {className} has no image");
        }

        byte[] pixels = texture.Format switch
        {
            EPixelFormat.PF_DXT5 => Bc3Pixels(data, mip.SizeX, mip.SizeY),
            EPixelFormat.PF_B8G8R8A8 => data,
            _ => throw new InvalidDataException($"The icon of {className} is {texture.Format}, not DXT5 or BGRA"),
        };

        using var bitmap = new SKBitmap(new SKImageInfo(mip.SizeX, mip.SizeY, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        Marshal.Copy(pixels, 0, bitmap.GetPixels(), mip.SizeX * mip.SizeY * 4);
        int size = Math.Min(IconSize, Math.Max(mip.SizeX, mip.SizeY));
        using SKBitmap scaled = size == mip.SizeX && size == mip.SizeY
            ? bitmap.Copy()
            : bitmap.Resize(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Unpremul), new SKSamplingOptions(SKCubicResampler.Mitchell));
        using SKData png = scaled.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    private static byte[] Bc3Pixels(byte[] data, int width, int height)
    {
        Bc3.Decompress<ColorBGRA<byte>, byte>(data, width, height, out byte[] pixels);
        return pixels;
    }

    /// <summary>The map's textures are the ones named Map_column-row; the folder holds a test gradient too.</summary>
    private static bool IsMapTexture(string path) =>
        path.StartsWith(MapTextures, StringComparison.OrdinalIgnoreCase)
        && path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
        && Path.GetFileName(path).StartsWith("Map_", StringComparison.Ordinal)
        && Path.GetFileName(path).Contains('-', StringComparison.Ordinal);

    /// <summary>Opens the game's files with the patched type map, and reads what <paramref name="read"/> asks.</summary>
    private T Read<T>(Func<DefaultFileProvider, T> read)
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
            return read(provider);
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
