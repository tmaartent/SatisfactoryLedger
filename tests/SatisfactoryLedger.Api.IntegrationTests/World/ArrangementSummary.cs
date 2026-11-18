namespace SatisfactoryLedger.Api.IntegrationTests.World;

public sealed record ArrangementSummary(
    Guid Id, Guid SnapshotId, string World, WorldRectangle? Area, WorldRectangle[] Exclusions, double PlotWidthMetres, double PlotDepthMetres,
    double RoadXMetres, double RoadYMetres, int Together, int Seconds, string Status, double? CostNow, double? Cost, string? Quality, string? Error);
