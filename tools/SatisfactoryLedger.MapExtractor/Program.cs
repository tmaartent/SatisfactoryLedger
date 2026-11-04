using SatisfactoryLedger.MapExtractor;

// Writes the resource node table from a game install (ADR-0015). Run it after a game update, then import the
// table into the Catalog. Exit code 0 on success, 1 on wrong arguments, 2 when the game files can't be read.

string? game = Option("--game");
string? gameVersion = Option("--game-version");
string output = Option("--out") ?? "resource-nodes.json";

if (game is null || gameVersion is null)
{
    Console.Error.WriteLine("""
        Writes the resource node table (resource-nodes.json) from a Satisfactory install.

          --game <folder>          the game's install folder, e.g. "C:\Program Files (x86)\Steam\steamapps\common\Satisfactory"
          --game-version <label>   the game version the table belongs to, e.g. 1.2
          --out <file>             where to write the table (default: resource-nodes.json)
        """);
    return 1;
}

var map = new GameMap(game);
if (map.Problem() is { } problem)
{
    Console.Error.WriteLine(problem);
    return 2;
}

ResourceNodeTable table;
try
{
    table = ResourceNodeTable.Build(gameVersion, map.ResourceNodes());
}
catch (Exception e) when (e is InvalidDataException or NotSupportedException)
{
    // Fail clearly rather than write a partial table (ADR-0015).
    Console.Error.WriteLine($"The game files can't be read into a node table: {e.Message}");
    return 2;
}

await File.WriteAllTextAsync(output, table.ToJson());
Console.WriteLine($"{table.Nodes.Count} resource nodes written to {Path.GetFullPath(output)}");
foreach (var group in table.Nodes.GroupBy(node => node.Purity).OrderBy(group => group.Key))
{
    Console.WriteLine($"  {group.Key}: {group.Count()}");
}

return 0;

string? Option(string name)
{
    int index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
