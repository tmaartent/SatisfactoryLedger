using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Domain.Arrangement;
using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Arrangement;

/// <summary>A run's problem, made from its snapshot, the snapshot's analysis and its world's factories.</summary>
internal static class ArrangementProblems
{
    /// <summary>The run's whole problem, its links weighed by the analysis: what the analysis service solves.</summary>
    public static async Task<MadeArrangement> MakeAsync(
        WorldDbContext db, StoredArrangement run, SnapshotDocument snapshot, string analysis, CancellationToken cancellationToken)
    {
        return WorldArrangement.Of(snapshot, AnalysisDocument.FromJson(analysis), await FactoriesAsync(db, run, cancellationToken), run.Settings);
    }

    /// <summary>Where things stand in the run's area, without links: enough to tell whether the factories fit.</summary>
    public static async Task<ArrangementProblem> PlacingAsync(WorldDbContext db, StoredArrangement run, SnapshotDocument snapshot, CancellationToken cancellationToken) =>
        WorldArrangement.Placing(snapshot, await FactoriesAsync(db, run, cancellationToken), run.Settings);

    private static Task<List<Factories.StoredFactory>> FactoriesAsync(WorldDbContext db, StoredArrangement run, CancellationToken cancellationToken) =>
        db.Factories.AsNoTracking()
            .Where(factory => factory.World == run.World)
            .OrderBy(factory => factory.Id)
            .ToListAsync(cancellationToken);

    /// <summary>Why factories can't fit: how many there are against the free plots, and what to change.</summary>
    public static string NoRoom(ArrangementProblem problem)
    {
        int free = problem.FreePlots;
        return $"{problem.Factories.Count} factories but {free} free {(free == 1 ? "plot" : "plots")} in the area: "
            + "make the plots or roads smaller, the area larger, or keep less out.";
    }
}
