using System.Text.Json;
using System.Text.Json.Serialization;

namespace SatisfactoryLedger.MapExtractor;

/// <summary>
/// The node table the Catalog imports (ADR-0015): one entry per resource node, with the game's quirks translated
/// (<c>RP_Inpure</c>, "not stored" meaning normal). Nothing of the game's format gets past this class.
/// </summary>
internal sealed record ResourceNodeTable(int Version, string GameVersion, IReadOnlyList<ResourceNodeEntry> Nodes)
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static ResourceNodeTable Build(string gameVersion, IEnumerable<MapNode> nodes)
    {
        List<ResourceNodeEntry> entries = [.. nodes.Select(ToEntry).OrderBy(entry => entry.Name, StringComparer.Ordinal)];

        string? duplicate = entries.GroupBy(entry => entry.Name).FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicate is not null)
        {
            throw new InvalidDataException($"The map has more than one node named {duplicate}");
        }

        return new ResourceNodeTable(CurrentVersion, gameVersion, entries);
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    private static ResourceNodeEntry ToEntry(MapNode node)
    {
        if (string.IsNullOrWhiteSpace(node.ResourceClass))
        {
            throw new InvalidDataException($"Node {node.Name} has no resource");
        }

        return new ResourceNodeEntry(node.Name, node.Kind, ClassName(node.ResourceClass), PurityOf(node), node.Position);
    }

    /// <summary><c>BlueprintGeneratedClass'/Game/…/Desc_OreIron.Desc_OreIron_C'</c> becomes <c>Desc_OreIron_C</c>.</summary>
    private static string ClassName(string resourceClass) =>
        resourceClass.TrimEnd('\'')[(resourceClass.TrimEnd('\'').LastIndexOf('.') + 1)..];

    /// <summary>
    /// Unreal stores only values that differ from the default. Impure and pure are stored, so "not stored" is normal.
    /// The game spells impure <c>RP_Inpure</c>. A value may come with its enum name: <c>EResourcePurity::RP_Pure</c>.
    /// </summary>
    private static Purity PurityOf(MapNode node) => node.Purity?[(node.Purity.LastIndexOf(':') + 1)..] switch
    {
        null => Purity.Normal,
        "RP_Inpure" => Purity.Impure,
        "RP_Normal" => Purity.Normal,
        "RP_Pure" => Purity.Pure,
        string unknown => throw new InvalidDataException($"Node {node.Name} has an unknown purity {unknown}"),
    };
}