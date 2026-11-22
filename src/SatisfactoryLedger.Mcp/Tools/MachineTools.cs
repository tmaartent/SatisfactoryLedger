using System.ComponentModel;

using ModelContextProtocol;
using ModelContextProtocol.Server;

using SatisfactoryLedger.Mcp.Answers;
using SatisfactoryLedger.Mcp.Catalog;
using SatisfactoryLedger.Mcp.LedgerApi;

namespace SatisfactoryLedger.Mcp.Tools;

/// <summary>Single machines, looked up by what they make, what they are, or how they run.</summary>
[McpServerToolType]
internal sealed class MachineTools(LedgerClient ledger, SnapshotChoice snapshots, GameDataSource gameData)
{
    private static readonly string[] Statuses = ["running", "starved", "outputBlocked", "noRecipe"];

    [McpServerTool(Name = "find_machines", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "The machines in a snapshot that make an item, are a building, or run with a status; the slowest first. Each with its recipe, " +
        "utilisation, the items it's short of or blocked on (shares in percent), and where it stands in metres. Give at least one filter.")]
    public async Task<MachinesFound> FindMachinesAsync(
        [Description("An item they make, by its in-game name, e.g. \"Iron Plate\".")] string? makes = null,
        [Description("A building, by its in-game name, e.g. \"Constructor\".")] string? building = null,
        [Description("running, starved, outputBlocked or noRecipe.")] string? status = null,
        [Description("The snapshot's id from list_snapshots; leave it out for the newest.")] string? snapshotId = null,
        [Description("How many to give at most; 25 when left out.")] int limit = 25,
        CancellationToken cancellationToken = default)
    {
        if (makes is null && building is null && status is null)
        {
            throw new McpException("Say which machines: what they make, which building, or their status.");
        }

        if (status is not null && !Statuses.Contains(status))
        {
            throw new McpException($"There's no status \"{status}\": ask for {string.Join(", ", Statuses)}.");
        }

        GameData game = await gameData.GetAsync(cancellationToken);
        IReadOnlySet<string>? recipes = makes is null ? null : game.RecipesMaking(game.FindItem(makes).Id);
        string? className = building is null ? null : game.FindBuilding(building).Id;
        SnapshotSummaryResponse snapshot = await snapshots.ChooseAsync(snapshotId, cancellationToken);
        AnalysisResponse analysis = await ledger.GetAnalysisAsync(snapshot.Id, cancellationToken);

        List<MachineFinding> found =
        [
            .. analysis.Machines
                .Where(m => recipes is null || (m.Recipe is not null && recipes.Contains(m.Recipe)))
                .Where(m => className is null || m.ClassName == className)
                .Where(m => status is null || m.Limit == MachineFacts.Limit(status))
                .OrderBy(m => m.Utilisation),
        ];
        return new MachinesFound(found.Count, [.. found.Take(Math.Max(1, limit)).Select(m => MachineFacts.Answer(m, game))]);
    }
}
