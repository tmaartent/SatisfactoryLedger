using System.ComponentModel;

using ModelContextProtocol.Server;

using SatisfactoryLedger.Mcp.Answers;
using SatisfactoryLedger.Mcp.Catalog;
using SatisfactoryLedger.Mcp.LedgerApi;

namespace SatisfactoryLedger.Mcp.Tools;

/// <summary>Where a shortage or an output blockage starts (decided 2026-11-20), for one kind of machine.</summary>
[McpServerToolType]
internal sealed class TraceTools(LedgerClient ledger, SnapshotChoice snapshots, GameDataSource gameData)
{
    private const string MoreCapacityText =
        "'moreCapacity' says what carries all that's offered to it: the lowest tier that does, or that many lines of the highest tier side by side";

    [McpServerTool(Name = "trace_upstream", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Where the shortage of starved machines starts. Follows what the machines of a building (and recipe) are short of upstream, through " +
        "producers that are starved themselves, to the first cause. Each start gives its cause as a code and, in causeTerm, as the ledger's " +
        $"term: {TraceCauses.TooFewProducers} (they run at full speed and all of it arrives), {TraceCauses.Bottleneck} (a belt, lift or pipe " +
        $"at capacity; {MoreCapacityText}), {TraceCauses.SourceAtLimit} (a miner or extractor gives all it can), {TraceCauses.TakenByOthers} (enough is made, but " +
        $"other machines get part of it first; 'others' says who and how much), {TraceCauses.ProducerOutputBlocked} (the producers can't get " +
        $"their own output away), {TraceCauses.UnknownSupply} (a station or container), {TraceCauses.NothingBringsIt}, " +
        $"{TraceCauses.WithoutARecipe}, {TraceCauses.Unclear}. Each start comes with the path of steps from the machines asked about.")]
    public Task<TraceAnswer> TraceUpstreamAsync(
        [Description("The building, by its in-game name, e.g. \"Constructor\".")] string building,
        [Description("Its recipe, by its in-game name, e.g. \"Iron Plate\"; leave it out for every recipe of the building.")] string? recipe = null,
        [Description("The snapshot's id from list_snapshots; leave it out for the newest.")] string? snapshotId = null,
        CancellationToken cancellationToken = default) =>
        TraceAsync("shortage", "starved", building, recipe, snapshotId, cancellationToken);

    [McpServerTool(Name = "trace_downstream", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Where the blockage of output-blocked machines starts. Follows what the machines of a building (and recipe) can't get away downstream, " +
        "through consumers that are blocked themselves, to the first cause. Each start gives its cause as a code and, in causeTerm, as the " +
        $"ledger's term: {TraceCauses.TooFewConsumers} (they run at full speed on all that's sent), {TraceCauses.Bottleneck} (a belt, lift or " +
        $"pipe at capacity; {MoreCapacityText}), {TraceCauses.SuppliedByOthers} (the consumers also take from other producers; 'others' says who and how much), " +
        $"{TraceCauses.ConsumerStarved} (they lack another input; 'shortOf' says which, in percent), {TraceCauses.StorageFull} (a full " +
        $"container or station), {TraceCauses.NowhereToGo} (a belt to nothing, or no line out at all), {TraceCauses.WithoutARecipe}, " +
        $"{TraceCauses.Unclear}. Each start comes with the path.")]
    public Task<TraceAnswer> TraceDownstreamAsync(
        [Description("The building, by its in-game name, e.g. \"Smelter\".")] string building,
        [Description("Its recipe, by its in-game name, e.g. \"Iron Ingot\"; leave it out for every recipe of the building.")] string? recipe = null,
        [Description("The snapshot's id from list_snapshots; leave it out for the newest.")] string? snapshotId = null,
        CancellationToken cancellationToken = default) =>
        TraceAsync("blockage", "output blocked", building, recipe, snapshotId, cancellationToken);

    private async Task<TraceAnswer> TraceAsync(string direction, string held, string building, string? recipe, string? snapshotId, CancellationToken cancellationToken)
    {
        GameData game = await gameData.GetAsync(cancellationToken);
        BuildableResponse machine = game.FindBuilding(building);
        RecipeResponse? chosen = recipe is null ? null : game.FindRecipe(machine, recipe);
        SnapshotSummaryResponse snapshot = await snapshots.ChooseAsync(snapshotId, cancellationToken);
        TraceResponse trace = await ledger.GetTraceAsync(snapshot.Id, direction, machine.Id, chosen?.Id, cancellationToken);

        string kind = chosen is null ? machine.Name : $"{machine.Name} ({chosen.Name})";
        string? note = trace.Machines == 0 ? $"There's no {kind} in this snapshot."
            : trace.Traced == 0 ? $"None of the {trace.Machines} {kind} machines is {held}: there's nothing to trace."
            : null;
        bool upstream = direction == "shortage";
        return new TraceAnswer(machine.Name, chosen?.Name, trace.Machines, trace.Traced, [.. trace.Starts.Select(start => Start(start, upstream, game))], note);
    }

    private static TraceStartAnswer Start(TraceStartResponse start, bool upstream, GameData game)
    {
        string item = game.Item(start.Item);
        return new TraceStartAnswer(
            start.Cause,
            TraceCauses.TermOf(start.Cause),
            item,
            [.. start.At.Select(group => Buildings(group, game))],
            [
                .. start.Path.SelectMany(step => step.Machines.Select(group => new TraceStepAnswer(
                    game.Building(group.ClassName),
                    game.Recipe(group.Recipe),
                    group.Count,
                    group.Ids,
                    upstream ? game.Item(step.Item) : null,
                    upstream ? null : game.Item(step.Item),
                    step.PerMinute))),
            ],
            start.PerMinute,
            start.Others.Count == 0 ? null : [.. start.Others.Select(group => Buildings(group, game))],
            MachineFacts.Shares(start.ShortOf, game),
            start.MoreCapacity is { } more ? new MoreCapacityAnswer(game.Building(more.Tier), more.Lines, more.Carries) : null);
    }

    private static BuildingsAnswer Buildings(TraceGroupResponse group, GameData game) => new(
        game.Building(group.ClassName),
        game.Recipe(group.Recipe),
        group.Count,
        group.PerMinute > 0 ? group.PerMinute : null,
        group.Ids,
        Metres.Of(group.Position));
}
