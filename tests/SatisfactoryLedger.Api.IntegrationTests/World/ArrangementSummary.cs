namespace SatisfactoryLedger.Api.IntegrationTests.World;

public sealed record ArrangementSummary(
    Guid Id, Guid SnapshotId, string World, WorldRectangle? Area, WorldRectangle[] Exclusions, double PlotWidthMetres, double PlotDepthMetres,
    double RoadXMetres, double RoadYMetres, int Together, double CeilingMetres, bool SupplyLines, int Seconds, string Status, double? CostNow, double? Cost, string? Quality, string? Error,
    double? BeltMetres, int? LevelsUsed, int? UnroutedBelts, int? Placements, int? LastResortCrossings);
