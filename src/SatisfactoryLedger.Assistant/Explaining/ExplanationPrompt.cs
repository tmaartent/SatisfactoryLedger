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
    /// ledger's term alone (the MCP server's causeTerm), so the model doesn't repeat the code for it.
    /// </summary>
    private static string ForTheModel(JsonElement answer)
    {
        JsonNode node = JsonNode.Parse(answer.GetRawText())!;
        Simplify(node);
        return node.ToJsonString();
    }

    private static void Simplify(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject properties:
                properties.Remove("ids");
                properties.Remove("positionMetres");
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

    /// <summary>The machines asked about as the trace names them: "Constructor (Iron Plate)".</summary>
    private static string Kind(JsonElement trace)
    {
        string building = trace.GetProperty("building").GetString()!;
        return trace.TryGetProperty("recipe", out JsonElement recipe) && recipe.ValueKind == JsonValueKind.String
            ? $"{building} ({recipe.GetString()})"
            : building;
    }
}
