namespace SatisfactoryLedger.World.Arrangement;

/// <summary>
/// A run of the optimal arrangement, as stored (decided 2026-11-12): every run is kept until it's deleted, per world, with its settings,
/// the problem it solved and the proposal that came back. Computed by the analysis service (the ADR-0018 pattern).
/// </summary>
internal sealed class StoredArrangement
{
    public required Guid Id { get; init; }

    /// <summary>The session name of the world it's for.</summary>
    public required string World { get; init; }

    /// <summary>The snapshot it was made from: its positions, factories as traced there, and the flows of its analysis.</summary>
    public required Guid SnapshotId { get; init; }

    /// <summary>The area drawn on the map, in the game's centimetres; null for everything built.</summary>
    public double? AreaX0 { get; init; }

    public double? AreaY0 { get; init; }

    public double? AreaX1 { get; init; }

    public double? AreaY1 { get; init; }

    public required double PlotWidthMetres { get; init; }

    public required double PlotDepthMetres { get; init; }

    public required double RoadXMetres { get; init; }

    public required double RoadYMetres { get; init; }

    /// <summary>How strongly the arranged factories are kept together (BR-O1); 0 in runs from before it existed.</summary>
    public required int Together { get; init; }

    /// <summary>How much belts to nodes, stations and drone ports count in placing (BR-O1); 0 in runs from before it existed.</summary>
    public required double SpotWeight { get; init; }

    /// <summary>How high the belt floor below the factories is (BR-O2); 32 in runs from before it existed.</summary>
    public required double CeilingMetres { get; init; }

    /// <summary>Whether belts are routed as supply lines (BR-O2); not in runs from before them.</summary>
    public bool SupplyLines { get; init; }

    /// <summary>Whether each factory takes its footprint instead of one plot (BR-O1); not in runs from before it.</summary>
    public bool Footprints { get; init; }

    /// <summary>Where no factory may stand, in the game's centimetres; none in runs from before they existed.</summary>
    public List<WorldRectangle> Exclusions { get; init; } = [];

    /// <summary>How long the solver may take.</summary>
    public required int Seconds { get; init; }

    public ArrangementRunStatus Status { get; set; }

    public required DateTimeOffset RequestedAt { get; init; }

    public DateTimeOffset? ComputedAt { get; set; }

    /// <summary>How long the solver took.</summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>Why it failed, when it did.</summary>
    public string? Error { get; set; }

    /// <summary>The grid's lowest corner in the game's centimetres, set when the problem is made.</summary>
    public double? GridOriginX { get; set; }

    public double? GridOriginY { get; set; }

    /// <summary>The <c>ArrangementProblemDocument</c> it solved, as JSON.</summary>
    public string? Problem { get; set; }

    /// <summary>The <c>ArrangementProposalDocument</c> that came back, as JSON.</summary>
    public string? Proposal { get; set; }

    /// <summary>The area drawn, or null for everything built.</summary>
    public WorldRectangle? Area => AreaX0 is { } x0 && AreaY0 is { } y0 && AreaX1 is { } x1 && AreaY1 is { } y1 ? new WorldRectangle(x0, y0, x1, y1) : null;

    public ArrangementSettings Settings => new(Area, PlotWidthMetres, PlotDepthMetres, RoadXMetres, RoadYMetres, Together, SpotWeight, CeilingMetres, Exclusions, SupplyLines, Footprints);

    /// <summary>The plots its problem was made on; null until it's made.</summary>
    public ArrangementGrid? Grid => GridOriginX is { } x && GridOriginY is { } y ? ArrangementGrid.Of(x, y, Settings) : null;
}
