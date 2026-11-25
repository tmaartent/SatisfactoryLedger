using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Json.Schema;

namespace SatisfactoryLedger.Mcp.Tests;

/// <summary>
/// The API as api/openapi.json describes it (step 99): what it answers on a path, per status. The build writes that file
/// from the API's code (ADR-0006), so a fake answer that fits it is one the real API could give. Its schemas are JSON
/// Schema (OpenAPI 3.1), checked with JsonSchema.Net.
/// </summary>
internal static class ApiDescription
{
    private static readonly JsonObject Document =
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "openapi.json")))!.AsObject();

    /// <summary>
    /// Why the API would never answer this to a GET of the path and query: a path it doesn't have, a status it doesn't
    /// answer there, or a body that doesn't fit; nothing when it could.
    /// </summary>
    public static string? Misfit(string pathAndQuery, HttpStatusCode status, string json)
    {
        string path = pathAndQuery.Split('?')[0];
        var operation = Document["paths"]!.AsObject()
            .Where(entry => Regex.IsMatch(path, "^" + Regex.Replace(entry.Key, "{[^}]+}", "[^/]+") + "$"))
            .Select(entry => entry.Value?["get"])
            .FirstOrDefault(get => get is not null);
        if (operation is null)
        {
            return $"The API has no GET {path}.";
        }

        JsonNode? schema = operation["responses"]?[((int)status).ToString(CultureInfo.InvariantCulture)]?["content"]?["application/json"]?["schema"];
        if (schema is null)
        {
            return $"GET {path} doesn't answer {(int)status} with JSON.";
        }

        // The response's schema, with the API's schemas beside it, so its "#/components/schemas/..." refs resolve.
        JsonObject whole = schema.DeepClone() as JsonObject ?? [];
        whole["$schema"] = "https://json-schema.org/draft/2020-12/schema";
        whole["components"] = Document["components"]!.DeepClone();
        JsonSchema check = JsonSchema.Build(
            JsonSerializer.SerializeToElement(whole),
            new BuildOptions { SchemaRegistry = new SchemaRegistry() },
            new Uri($"https://api.test/{Guid.NewGuid()}"));

        EvaluationResults results = check.Evaluate(
            JsonDocument.Parse(json).RootElement,
            new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        return results.IsValid
            ? null
            : $"GET {pathAndQuery} answering {(int)status} doesn't fit the API: " + string.Join("; ", Errors(results).Distinct().Take(10));
    }

    /// <summary>
    /// What's wrong, from the parts that failed only: a oneOf that holds still has a failed branch, as a position that
    /// isn't null, which isn't wrong.
    /// </summary>
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
