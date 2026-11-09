using System.Globalization;
using System.IO.Compression;

using SatisfactoryLedger.World.Domain.Flow;
using SatisfactoryLedger.World.Domain.Flow.Objects;

namespace SatisfactoryLedger.World.Domain.Tests;

/// <summary>
/// Reads a flow network exported as text, one line each:
/// <code>
/// F|fluid;fluid                          the items that are fluids
/// N|provider|id|item=offer
/// N|unknown|id|carries|item=stock;…|fuel;…
/// N|machine|id|item=demand;…|item=production;…
/// N|conduit|id|capacity|carries
/// N|splitter|id|carries|output:kind:item;…
/// N|merger|id|carries
/// N|sink|id|fuel;…
/// E|from|to|fromPort|toPort
/// </code>
/// The step 15 probe wrote it from a real world; the format is for test fixtures only.
/// </summary>
internal static class FlowText
{
    public static FlowNetwork ReadGzip(string path)
    {
        using var file = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);
        using var reader = new StreamReader(file);
        return Read(reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    public static FlowNetwork Read(IReadOnlyList<string> lines)
    {
        HashSet<ItemId> fluids = [.. lines[0].TrimEnd('\r')[2..].Split(';', StringSplitOptions.RemoveEmptyEntries).Select(ItemId.Of)];
        List<FlowNode> nodes = [];
        List<Connection> connections = [];
        foreach (string[] f in lines.Skip(1).Select(line => line.TrimEnd('\r').Split('|')))
        {
            if (f[0] == "E")
            {
                connections.Add(new Connection(BuildableId.Of(f[1]), BuildableId.Of(f[2]), Port(f[3]), Port(f[4])));
                continue;
            }

            var id = BuildableId.Of(f[2]);
            nodes.Add(f[1] switch
            {
                "provider" => new FlowProvider(id, ItemId.Of(f[3].Split('=')[0]), Number(f[3].Split('=')[1])),
                "unknown" => new FlowUnknownSource(id, Rates(f[4]), Enum.Parse<Carries>(f[3]), Items(f[5])),
                "machine" => new FlowMachine(id, Rates(f[3]), Rates(f[4])),
                "conduit" => new FlowConduit(id, Number(f[3]), Enum.Parse<Carries>(f[4])),
                "splitter" => new FlowSplitter(id, Enum.Parse<Carries>(f[3]), Rules(f[4])),
                "merger" => new FlowMerger(id, Enum.Parse<Carries>(f[3])),
                "sink" => new FlowSink(id, Items(f[3])),
                _ => throw new FormatException($"Unknown kind of node: {f[1]}"),
            });
        }

        return new FlowNetwork(nodes, connections, fluids);
    }

    private static double Number(string value) =>
        value is "∞" or "Infinity" ? double.PositiveInfinity : double.Parse(value, CultureInfo.InvariantCulture);

    private static int? Port(string value) => value.Length == 0 ? null : int.Parse(value, CultureInfo.InvariantCulture);

    private static Dictionary<ItemId, double> Rates(string text) => text.Length == 0
        ? []
        : text.Split(';').Select(entry => entry.Split('=')).ToDictionary(parts => ItemId.Of(parts[0]), parts => Number(parts[1]));

    private static HashSet<ItemId>? Items(string text) => text.Length == 0 ? null : [.. text.Split(';').Select(ItemId.Of)];

    private static List<SortRule>? Rules(string text) => text.Length == 0
        ? null
        : [.. text.Split(';').Select(rule => rule.Split(':')).Select(parts => new SortRule(
            int.Parse(parts[0], CultureInfo.InvariantCulture),
            Enum.Parse<SortRuleKind>(parts[1]),
            parts[2].Length == 0 ? null : ItemId.Of(parts[2])))];
}
