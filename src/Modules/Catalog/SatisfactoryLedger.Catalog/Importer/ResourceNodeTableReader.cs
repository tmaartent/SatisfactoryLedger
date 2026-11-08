using System.Text.Json;

using SatisfactoryLedger.Catalog.Domain;

namespace SatisfactoryLedger.Catalog.Importer;

/// <summary>
/// Reads the node table the map tool writes (ADR-0015). The table is our own format, version 1; anything else is
/// refused with an <see cref="InvalidDataException"/>, never half read.
/// </summary>
internal static class ResourceNodeTableReader
{
    private const int SupportedVersion = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<ResourceNodeTable> ReadAsync(Stream table, CancellationToken cancellationToken)
    {
        TableDto dto = await JsonSerializer.DeserializeAsync<TableDto>(table, Json, cancellationToken)
            ?? throw new InvalidDataException("The node table is empty");

        if (dto.Version != SupportedVersion)
        {
            throw new InvalidDataException($"Node table format version {dto.Version} is not supported, only {SupportedVersion}");
        }

        if (string.IsNullOrWhiteSpace(dto.GameVersion))
        {
            throw new InvalidDataException("The node table has no game version");
        }

        return new ResourceNodeTable(dto.GameVersion, [.. (dto.Nodes ?? []).Select(ToNode)]);
    }

    private static ResourceNode ToNode(NodeDto node)
    {
        if (string.IsNullOrWhiteSpace(node.Name) || string.IsNullOrWhiteSpace(node.Resource))
        {
            throw new InvalidDataException("A node in the table has no name or no resource");
        }

        PositionDto position = node.Position ?? throw new InvalidDataException($"Node {node.Name} has no position");

        return ResourceNode.Create(
            ResourceNodeId.Of(node.Name),
            node.Kind switch
            {
                "resourceNode" => ResourceNodeKind.ResourceNode,
                "wellSatellite" => ResourceNodeKind.WellSatellite,
                _ => throw new InvalidDataException($"Node {node.Name} has an unknown kind {node.Kind}"),
            },
            ItemId.Of(node.Resource),
            node.Purity switch
            {
                "impure" => Purity.Impure,
                "normal" => Purity.Normal,
                "pure" => Purity.Pure,
                _ => throw new InvalidDataException($"Node {node.Name} has an unknown purity {node.Purity}"),
            },
            new MapPosition(position.X, position.Y, position.Z));
    }

    private sealed record TableDto(int Version, string? GameVersion, List<NodeDto>? Nodes);

    private sealed record NodeDto(string? Name, string? Kind, string? Resource, string? Purity, PositionDto? Position);

    private sealed record PositionDto(double X, double Y, double Z);
}