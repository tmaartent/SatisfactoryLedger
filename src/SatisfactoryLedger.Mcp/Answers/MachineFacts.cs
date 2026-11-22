using SatisfactoryLedger.Mcp.Catalog;
using SatisfactoryLedger.Mcp.LedgerApi;

namespace SatisfactoryLedger.Mcp.Answers;

/// <summary>What the tools say about machines: their status, their shares by item name, and their groups.</summary>
internal static class MachineFacts
{
    /// <summary>The API's "none" is a machine nothing holds back: running, to a model.</summary>
    public static string Status(string limit) => limit == "none" ? "running" : limit;

    /// <summary>The status a model asks for, as the API's limit.</summary>
    public static string Limit(string status) => status == "running" ? "none" : status;

    /// <summary>
    /// Each item's share as the game shows a percentage: whole numbers, never 100% for less than all (as the web's
    /// limit text does). Nothing when there are none.
    /// </summary>
    public static IReadOnlyDictionary<string, int>? Shares(IEnumerable<KeyValuePair<string, double>>? shares, GameData game)
    {
        Dictionary<string, int> named = [];
        foreach ((string item, double share) in shares ?? [])
        {
            string name = game.Item(item);
            int percent = Math.Min(99, (int)Math.Floor(share * 100));
            named[name] = named.TryGetValue(name, out int worst) ? Math.Min(worst, percent) : percent;
        }

        return named.Count == 0 ? null : named;
    }

    public static MachineAnswer Answer(MachineFinding machine, GameData game) => new(
        machine.Id,
        game.Building(machine.ClassName),
        game.Recipe(machine.Recipe),
        Status(machine.Limit),
        Percent(machine.Utilisation),
        Shares(machine.StarvedOf, game),
        Shares(machine.BlockedOn, game),
        machine.OnStock is null ? null : game.Item(machine.OnStock),
        machine.StockMinutes is null ? null : Math.Round(machine.StockMinutes.Value),
        Metres.Of(machine.Position));

    /// <summary>
    /// The machines held back, grouped by building, recipe and cause; the groups losing the most output first, then the
    /// biggest. A model gets the pattern in a few lines, not hundreds of machines.
    /// </summary>
    public static IReadOnlyList<MachineGroup> BelowFullSpeed(IEnumerable<MachineFinding> machines, GameData game, int groups) =>
    [
        .. machines
            .Where(machine => machine.Limit != "none")
            .GroupBy(machine => (machine.ClassName, machine.Recipe, machine.Limit))
            .Select(group => new MachineGroup(
                game.Building(group.Key.ClassName),
                game.Recipe(group.Key.Recipe),
                Status(group.Key.Limit),
                group.Count(),
                Percent(group.Average(machine => machine.Utilisation)),
                Math.Round(group.Sum(machine => 1 - machine.Utilisation), 1),
                Shares(group.SelectMany(machine => machine.StarvedOf ?? new Dictionary<string, double>()), game),
                Shares(group.SelectMany(machine => machine.BlockedOn ?? new Dictionary<string, double>()), game)))
            .OrderByDescending(group => group.MachinesLost)
            .ThenByDescending(group => group.Machines)
            .Take(groups),
    ];

    private static double Percent(double fraction) => Math.Round(fraction * 100, 1);
}
