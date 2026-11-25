using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

using Json.Schema;

using ModelContextProtocol;
using ModelContextProtocol.Client;

using SatisfactoryLedger.Mcp;
using SatisfactoryLedger.Mcp.Answers;

namespace SatisfactoryLedger.Assistant.Tests;

/// <summary>
/// The real MCP server's trace tools (step 99), which <see cref="FakeLedgerTools"/> stand in for: what they're called,
/// what they take, and the shape of what they answer. A fake that drifts from them would let the Assistant's tests pass
/// against tools that no longer exist.
/// </summary>
internal static class RealLedgerTools
{
    /// <summary>What both trace tools answer, as the MCP server writes it: camelCase, without empty fields.</summary>
    private static readonly JsonSchema TraceAnswerSchema = Strict(
        JsonSchemaExporter.GetJsonSchemaAsNode(
            new JsonSerializerOptions(McpJsonUtilities.DefaultOptions),
            typeof(TraceAnswer)));

    /// <summary>Why the real tool couldn't give this answer; nothing when it could.</summary>
    public static string? Misfit(string tool, string json)
    {
        if (tool is not ("trace_upstream" or "trace_downstream"))
        {
            return $"The fake answers {tool}, which isn't a trace tool.";
        }

        EvaluationResults results = TraceAnswerSchema.Evaluate(
            JsonDocument.Parse(json).RootElement,
            new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        return results.IsValid ? null : $"{tool} couldn't answer this: " + string.Join("; ", Errors(results).Distinct().Take(10));
    }

    /// <summary>The tools of the real MCP server, as a model lists them. Listing them asks the API nothing.</summary>
    public static Task<IList<McpClientTool>> ListAsync() =>
        McpTools.ListAsync(builder => builder.AddLedgerMcp(), app => app.MapLedgerMcp());

    /// <summary>
    /// The exported schema, made as strict as the answers are: a field is there unless it can be empty (the server
    /// leaves out what's null), and no field the type doesn't have.
    /// </summary>
    private static JsonSchema Strict(JsonNode schema)
    {
        Tighten(schema);
        schema["$schema"] = "https://json-schema.org/draft/2020-12/schema";
        return JsonSchema.Build(JsonSerializer.SerializeToElement(schema), new BuildOptions { SchemaRegistry = new SchemaRegistry() });
    }

    private static void Tighten(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject part:
                if (part["properties"] is JsonObject properties)
                {
                    part["required"] = new JsonArray(properties
                        .Where(property => !CanBeEmpty(property.Value))
                        .Select(property => (JsonNode)property.Key)
                        .ToArray());
                    part["additionalProperties"] = false;
                }

                foreach (var child in part.ToList())
                {
                    Tighten(child.Value);
                }

                break;
            case JsonArray items:
                foreach (JsonNode? item in items)
                {
                    Tighten(item);
                }

                break;
        }
    }

    private static bool CanBeEmpty(JsonNode? property) =>
        property?["type"] is JsonArray types && types.Any(type => type?.GetValue<string>() == "null");

    private static IEnumerable<string> Errors(EvaluationResults results)
    {
        if (results.IsValid)
        {
            return [];
        }

        IEnumerable<string> own = (results.Errors ?? new Dictionary<string, string>())
            .Select(error => $"{results.InstanceLocation} {error.Value}");
        return own.Concat((results.Details ?? []).SelectMany(Errors));
    }
}
