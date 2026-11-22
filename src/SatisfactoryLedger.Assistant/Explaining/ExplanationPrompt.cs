using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.AI;

using SatisfactoryLedger.Assistant.LedgerTools;

namespace SatisfactoryLedger.Assistant.Explaining;

/// <summary>
/// What a model is told to explain a finding: rules that keep it to the facts, the tool's own description of what its
/// answer means, and the answer itself. Grounding, not knowledge: the model may not know the game, and needn't.
/// </summary>
internal static class ExplanationPrompt
{
    private const string Rules = """
        You explain one finding of the Satisfactory Ledger to the player whose factory it is. The ledger analyses a
        snapshot of their world in a model of it. You get the facts from one of the ledger's tools, as JSON.

        Rules:
        - Use only these facts. Don't add what you know or guess about the game.
        - Write buildings, recipes and items exactly as the facts name them, e.g. "Quantum Encoder (Superposition Oscillator)".
        - Copy every number from the facts. Never work out a new number. Rates are per minute.
        - Say the main cause in the ledger's term the facts give it ("taken by others") and the item it concerns,
          then one next step the player can take. Never write the tool's names for the causes.
        - A start with a nextStep: that is the next step for it. Give it as written, and suggest no other belt, lift or pipe.
        - Each start's way says what the machines asked about lack (or can't get away) and how that leads to the start.
          Name what they lack first, as the way does; don't skip to the start's item.
        - The player sees every start beside your text, as a list with links to the map: don't list the starts, and
          don't give positions.
        - The facts are the ledger's model of the world: say "in the model" or "the ledger finds", not that it is certain.
        - Two or three sentences of plain text: no headings, lists or Markdown.

        The tool's own description of what it answers:
        """;

    public static IReadOnlyList<ChatMessage> For(HeldBack limit, ToolCall facts)
    {
        string held = limit == HeldBack.Starved ? "starved" : "output blocked";
        string question = $"""
            Why are the {Kind(facts.Answer)} machines {held}?

            The facts, from {facts.Tool}:
            {ForTheModel(facts.Answer)}
            """;
        return [new ChatMessage(ChatRole.System, $"{Rules}\n{facts.Description}"), new ChatMessage(ChatRole.User, question)];
    }

    /// <summary>
    /// The answer without the machines' ids and positions: the dialog links those to the map, and they take most of a
    /// large trace's tokens. A prompt longer than the model's context loses its start, the rules. Each cause is the
    /// ledger's term alone (the MCP server's causeTerm), so the model doesn't repeat the code for it. A bottleneck's
    /// more capacity is a sentence, its nextStep: given the tier, lines and rate, a small model took the rate for the
    /// belt's capacity and the lines for how many there are now. The dialog shows the rate. A start's path is a sentence
    /// too, its way: given the steps, a small model skipped from the machines asked about to the start's item.
    /// </summary>
    private static string ForTheModel(JsonElement answer)
    {
        JsonNode node = JsonNode.Parse(answer.GetRawText())!;
        Simplify(node);

        // Not for HTML: "can't" stays as written, not ', for the model to read and copy.
        return node.ToJsonString(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    private static void Simplify(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject properties:
                properties.Remove("ids");
                properties.Remove("positionMetres");

                if (properties["path"] is JsonArray path)
                {
                    properties.Remove("path");
                    if (path.Count > 0)
                    {
                        properties["way"] = Way(path);
                    }
                }

                if (properties["moreCapacity"] is JsonObject more)
                {
                    properties.Remove("moreCapacity");
                    properties["nextStep"] = NextStep(more, properties["at"]?[0]?["building"]?.GetValue<string>());
                }

                if (properties["causeTerm"] is JsonValue term && term.TryGetValue(out string? words))
                {
                    properties.Remove("causeTerm");
                    properties["cause"] = words;
                }

                foreach (KeyValuePair<string, JsonNode?> property in properties)
                {
                    Simplify(property.Value);
                }

                break;
            case JsonArray items:
                foreach (JsonNode? item in items)
                {
                    Simplify(item);
                }

                break;
        }
    }

    /// <summary>
    /// What a bottleneck needs, as the step to take: another line beside it when it's of the tier needed already,
    /// otherwise a rebuild as the tier, in lines side by side when one isn't enough.
    /// </summary>
    private static string NextStep(JsonObject more, string? bottleneck)
    {
        string tier = more["tier"]!.GetValue<string>();
        int lines = more["lines"]!.GetValue<int>();
        return lines == 1 ? $"Rebuild it as {tier}."
            : tier == bottleneck ? $"Lay another {tier} beside it, for {lines} lines side by side."
            : $"Rebuild it as {lines} lines of {tier} side by side.";
    }

    /// <summary>
    /// The path to a start as a sentence: "The Assembler (Nobelisk) machines are short of Black Powder, made by the
    /// Assembler (Black Powder) machines, which are short of Sulfur." Downstream: what they can't get away, and to whom.
    /// The path lists a step's machines one kind at a time, each with the step's item.
    /// </summary>
    private static string Way(JsonArray path)
    {
        List<(string Item, List<string> Machines)> steps = [];
        bool upstream = path[0]?["shortOf"] is not null;
        foreach (JsonNode? step in path)
        {
            string item = (step?["shortOf"] ?? step?["cannotLeave"])?.GetValue<string>() ?? "";
            string building = step?["building"]?.GetValue<string>() ?? "";
            string machines = step?["recipe"]?.GetValue<string>() is { } recipe ? $"{building} ({recipe})" : building;
            if (steps.Count > 0 && steps[^1].Item == item)
            {
                steps[^1].Machines.Add(machines);
            }
            else
            {
                steps.Add((item, [machines]));
            }
        }

        string Held(string item) => upstream ? $"are short of {item}" : $"can't get {item} away";
        string way = $"The {string.Join(" and ", steps[0].Machines)} machines {Held(steps[0].Item)}";
        foreach ((string item, List<string> machines) in steps.Skip(1))
        {
            way += $"{(upstream ? ", made by" : ", to")} the {string.Join(" and ", machines)} machines, which {Held(item)}";
        }

        return way + ".";
    }

    /// <summary>The machines asked about as the trace names them: "Constructor (Iron Plate)".</summary>
    private static string Kind(JsonElement trace)
    {
        string building = trace.GetProperty("building").GetString()!;
        return trace.TryGetProperty("recipe", out JsonElement recipe) && recipe.ValueKind == JsonValueKind.String
            ? $"{building} ({recipe.GetString()})"
            : building;
    }
}
