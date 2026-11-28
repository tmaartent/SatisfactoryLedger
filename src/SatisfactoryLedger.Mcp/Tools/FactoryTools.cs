using System.ComponentModel;

using ModelContextProtocol;
using ModelContextProtocol.Server;

using SatisfactoryLedger.Mcp.Answers;
using SatisfactoryLedger.Mcp.Catalog;
using SatisfactoryLedger.Mcp.LedgerApi;

namespace SatisfactoryLedger.Mcp.Tools;

/// <summary>The factories the player defined (BR-W2) and their findings.</summary>
[McpServerToolType]
internal sealed class FactoryTools(LedgerClient ledger, SnapshotChoice snapshots, GameDataSource gameData)
{
    private const int FactoryGroups = 15;

    [McpServerTool(Name = "list_factories", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("The factories the player defined in a snapshot's world: their id, name and how many machines. Untraceable ones lost machines since.")]
    public async Task<IReadOnlyList<FactoryAnswer>> ListFactoriesAsync(
        [Description("The snapshot's id from list_snapshots; leave it out for the newest.")] string? snapshotId = null,
        CancellationToken cancellationToken = default)
    {
        SnapshotSummaryResponse snapshot = await snapshots.ChooseAsync(snapshotId, cancellationToken);
        return [.. (await FactoriesByNameAsync(snapshot.Id, cancellationToken)).Select(f => new FactoryAnswer(f.FactoryId, f.Name, f.Traceable, f.Machines.Count))];
    }

    [McpServerTool(Name = "get_factory_findings", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "One factory's findings: what it takes in and makes (now, at full speed, and how much is short, per minute), its power, and its groups " +
        "of machines losing the most output with the items they're short of or blocked on.")]
    public async Task<FactoryFindings> GetFactoryFindingsAsync(
        [Description("The factory's name or id, from list_factories.")] string factory,
        [Description("The snapshot's id from list_snapshots; leave it out for the newest.")] string? snapshotId = null,
        CancellationToken cancellationToken = default)
    {
        SnapshotSummaryResponse snapshot = await snapshots.ChooseAsync(snapshotId, cancellationToken);
        IReadOnlyList<FactoryTrace> factories = await FactoriesByNameAsync(snapshot.Id, cancellationToken);
        string wanted = factory.Trim();
        FactoryTrace chosen = factories.FirstOrDefault(f => f.FactoryId == wanted || string.Equals(f.Name, wanted, StringComparison.OrdinalIgnoreCase))
            ?? throw new McpException(factories.Count == 0
                ? "This world has no factories defined."
                : $"No factory is called \"{wanted}\". The factories are: {string.Join(", ", factories.Select(f => f.Name))}.");

        FactoryAnalysisResponse analysis = await ledger.GetFactoryAnalysisAsync(snapshot.Id, chosen.FactoryId, cancellationToken);
        GameData game = await gameData.GetAsync(cancellationToken);
        ItemFlowAnswer Flow(FactoryFlow flow) => new(game.Item(flow.Item), flow.Now, flow.AtFullSpeed, flow.Short);
        return new FactoryFindings(
            analysis.FactoryId,
            analysis.Name,
            MachineCounts.Of(analysis.Summary),
            [.. analysis.Input.Select(Flow)],
            [.. analysis.Output.Select(Flow)],
            analysis.Power,
            MachineFacts.BelowFullSpeed(analysis.Machines, game, FactoryGroups));
    }

    /// <summary>The world's factories, by name: the ledger gives them oldest first, an order only the web's colours use.</summary>
    private async Task<IReadOnlyList<FactoryTrace>> FactoriesByNameAsync(string snapshotId, CancellationToken cancellationToken) =>
        [.. (await ledger.GetFactoriesAsync(snapshotId, cancellationToken)).OrderBy(factory => factory.Name, StringComparer.CurrentCulture)];
}
