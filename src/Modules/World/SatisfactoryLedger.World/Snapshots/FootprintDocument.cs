namespace SatisfactoryLedger.World.Snapshots;

/// <summary>The ground a building class covers, seen from above, in its own coordinates (centimetres), as the Catalog said at import.</summary>
/// <param name="Volumes">The same boxes in 3D; null in a snapshot older than version 10, or empty when the Catalog had none.</param>
internal sealed record FootprintDocument(string Id, decimal MinX, decimal MinY, decimal MaxX, decimal MaxY, IReadOnlyList<VolumeDocument>? Volumes = null);