using Microsoft.EntityFrameworkCore;

using SatisfactoryLedger.World.Persistence;
using SatisfactoryLedger.World.Contracts.Analysis;

namespace SatisfactoryLedger.World.Analysis;

/// <summary>Stores what the analysis service sends back for a snapshot (ADR-0017, ADR-0018).</summary>
internal sealed class StoreAnalysisHandler(WorldDbContext db, TimeProvider clock)
{
    /// <returns>False when there's no such snapshot.</returns>
    public async Task<bool> StoreAsync(Guid snapshotId, AnalysisResult result, CancellationToken cancellationToken) =>
        await StoreAsync(snapshotId, stored =>
        {
            stored.Status = AnalysisStatus.Ready;
            stored.Version = result.Analysis.Version;
            stored.Content = result.Analysis.ToJson();
            stored.Duration = result.Duration;
            stored.Error = null;
        }, cancellationToken);

    /// <returns>False when there's no such snapshot.</returns>
    public async Task<bool> StoreFailureAsync(Guid snapshotId, string error, CancellationToken cancellationToken) =>
        await StoreAsync(snapshotId, stored =>
        {
            stored.Status = AnalysisStatus.Failed;
            stored.Version = AnalysisDocument.CurrentVersion;
            stored.Error = error;
        }, cancellationToken);

    private async Task<bool> StoreAsync(Guid snapshotId, Action<StoredAnalysis> update, CancellationToken cancellationToken)
    {
        if (await db.Snapshots.FindAsync([snapshotId], cancellationToken) is null)
        {
            return false;
        }

        StoredAnalysis? stored = await db.Analyses.FindAsync([snapshotId], cancellationToken);
        if (stored is null)
        {
            stored = StoredAnalysis.PendingFor(snapshotId);
            db.Analyses.Add(stored);
        }

        update(stored);
        stored.ComputedAt = clock.GetUtcNow();
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        // The snapshot was deleted meanwhile, taking its analysis with it: as if it was never there.
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}