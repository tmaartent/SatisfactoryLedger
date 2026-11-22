using ModelContextProtocol;

namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>Which snapshot a tool looks at: the one named, or the newest when none is.</summary>
internal sealed class SnapshotChoice(LedgerClient ledger)
{
    public async Task<SnapshotSummaryResponse> ChooseAsync(string? snapshotId, CancellationToken cancellationToken)
    {
        IReadOnlyList<SnapshotSummaryResponse> snapshots = await ledger.GetSnapshotsAsync(cancellationToken);
        if (snapshots.Count == 0)
        {
            throw new McpException("No save has been imported yet: there are no snapshots.");
        }

        // The API lists the newest first.
        return string.IsNullOrWhiteSpace(snapshotId)
            ? snapshots[0]
            : snapshots.FirstOrDefault(snapshot => string.Equals(snapshot.Id, snapshotId.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new McpException($"No snapshot has the id \"{snapshotId}\". Call list_snapshots for the ids there are, or leave it out for the newest.");
    }
}
