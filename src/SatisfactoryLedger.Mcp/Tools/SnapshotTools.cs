using System.ComponentModel;

using ModelContextProtocol;
using ModelContextProtocol.Server;

using SatisfactoryLedger.Mcp.Answers;
using SatisfactoryLedger.Mcp.Catalog;
using SatisfactoryLedger.Mcp.LedgerApi;

namespace SatisfactoryLedger.Mcp.Tools;

/// <summary>The snapshots of the player's saves and their findings.</summary>
[McpServerToolType]
internal sealed class SnapshotTools(LedgerClient ledger, SnapshotChoice snapshots, GameDataSource gameData)
{
    /// <summary>How many groups of machines a summary shows: enough for the pattern, small enough for a context window.</summary>
    private const int SummaryGroups = 15;

    private const string SnapshotIdHelp = "The snapshot's id from list_snapshots; leave it out for the newest.";

    [McpServerTool(Name = "list_snapshots", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("The imported saves (snapshots), newest first: their id, world, when the game was saved, and whether their findings are ready.")]
    public async Task<IReadOnlyList<SnapshotAnswer>> ListSnapshotsAsync(CancellationToken cancellationToken = default) =>
        [.. (await ledger.GetSnapshotsAsync(cancellationToken)).Select(s => new SnapshotAnswer(s.Id, s.SessionName, s.SavedAt, s.Analysis))];

    [McpServerTool(Name = "get_findings_summary", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Start here. A snapshot's findings in brief: how many machines run at full speed, are starved (short of an input) or output blocked " +
        "(a product can't all leave), and the groups of machines losing the most output, with the items they're short of or blocked on " +
        "(shares in percent). Rates are per minute. Then use list_findings, find_machines or get_factory_findings for detail.")]
    public async Task<FindingsSummary> GetFindingsSummaryAsync(
        [Description(SnapshotIdHelp)] string? snapshotId = null,
        CancellationToken cancellationToken = default)
    {
        SnapshotSummaryResponse snapshot = await snapshots.ChooseAsync(snapshotId, cancellationToken);
        AnalysisResponse analysis = await ledger.GetAnalysisAsync(snapshot.Id, cancellationToken);
        GameData game = await gameData.GetAsync(cancellationToken);
        return new FindingsSummary(
            snapshot.Id,
            snapshot.SessionName,
            snapshot.SavedAt,
            MachineCounts.Of(analysis.Summary),
            analysis.Bottlenecks.Count,
            analysis.UnusedSupply.Count,
            analysis.UnknownSupply.Count,
            MachineFacts.BelowFullSpeed(analysis.Machines, game, SummaryGroups));
    }

    [McpServerTool(Name = "list_findings", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "The findings of one kind in a snapshot, the biggest first. Kinds: \"bottlenecks\" (belts and pipes at capacity while more is offered), " +
        "\"unusedSupply\" (miners, extractors and stations that could deliver more than is taken), \"unknownSupply\" (stations whose supply " +
        "the save doesn't say). Rates are per minute, positions in metres.")]
    public async Task<FindingsPage> ListFindingsAsync(
        [Description("bottlenecks, unusedSupply or unknownSupply.")] string kind,
        [Description(SnapshotIdHelp)] string? snapshotId = null,
        [Description("How many to give at most; 20 when left out.")] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        SnapshotSummaryResponse snapshot = await snapshots.ChooseAsync(snapshotId, cancellationToken);
        AnalysisResponse analysis = await ledger.GetAnalysisAsync(snapshot.Id, cancellationToken);
        GameData game = await gameData.GetAsync(cancellationToken);
        IReadOnlyList<object> findings = kind switch
        {
            "bottlenecks" =>
            [
                .. analysis.Bottlenecks.OrderByDescending(b => b.OfferedMore)
                    .Select(b => new BottleneckAnswer(b.Id, game.Building(b.ClassName), b.Flow, b.Capacity, b.OfferedMore, Metres.Of(b.Position))),
            ],
            "unusedSupply" =>
            [
                .. analysis.UnusedSupply.OrderByDescending(u => u.Offered - u.Delivered)
                    .Select(u => new UnusedSupplyAnswer(
                        u.Id, game.Building(u.ClassName), game.Item(u.Item), u.Offered, u.Delivered, u.Offered - u.Delivered, Metres.Of(u.Position))),
            ],
            "unknownSupply" =>
            [
                .. analysis.UnknownSupply.Select(u => new UnknownSupplyAnswer(
                    u.Id, game.Building(u.ClassName), u.Delivered.ToDictionary(d => game.Item(d.Key), d => d.Value), Metres.Of(u.Position))),
            ],
            _ => throw new McpException($"There's no kind of finding called \"{kind}\": ask for bottlenecks, unusedSupply or unknownSupply."),
        };
        return new FindingsPage(kind, findings.Count, [.. findings.Take(Math.Max(1, limit))]);
    }
}
