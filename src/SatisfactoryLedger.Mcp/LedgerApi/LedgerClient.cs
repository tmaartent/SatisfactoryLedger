using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using ModelContextProtocol;

namespace SatisfactoryLedger.Mcp.LedgerApi;

/// <summary>
/// The ledger's public API, as a typed HttpClient: the MCP server reads it as any client would (ADR-0003 keeps it out
/// of the modules). What the API can't answer becomes an <see cref="McpException"/>, whose message the model sees.
/// </summary>
internal sealed class LedgerClient(HttpClient http)
{
    public Task<IReadOnlyList<SnapshotSummaryResponse>> GetSnapshotsAsync(CancellationToken cancellationToken) =>
        GetListAsync<SnapshotSummaryResponse>("/api/v1/world/snapshots", cancellationToken);

    public Task<AnalysisResponse> GetAnalysisAsync(string snapshotId, CancellationToken cancellationToken) =>
        GetAnalysedAsync<AnalysisResponse>($"/api/v1/world/snapshots/{Uri.EscapeDataString(snapshotId)}/analysis", "snapshot", cancellationToken);

    public Task<IReadOnlyList<FactoryTrace>> GetFactoriesAsync(string snapshotId, CancellationToken cancellationToken) =>
        GetListAsync<FactoryTrace>($"/api/v1/world/snapshots/{Uri.EscapeDataString(snapshotId)}/factories", cancellationToken);

    public Task<FactoryAnalysisResponse> GetFactoryAnalysisAsync(string snapshotId, string factoryId, CancellationToken cancellationToken) =>
        GetAnalysedAsync<FactoryAnalysisResponse>(
            $"/api/v1/world/snapshots/{Uri.EscapeDataString(snapshotId)}/factories/{Uri.EscapeDataString(factoryId)}/analysis", "factory", cancellationToken);

    /// <param name="direction">"shortage" (upstream) or "blockage" (downstream).</param>
    /// <param name="recipe">Only the machines with this recipe (its class name); all of the building when null.</param>
    public Task<TraceResponse> GetTraceAsync(string snapshotId, string direction, string building, string? recipe, CancellationToken cancellationToken) =>
        GetAnalysedAsync<TraceResponse>(
            $"/api/v1/world/snapshots/{Uri.EscapeDataString(snapshotId)}/traces/{direction}?building={Uri.EscapeDataString(building)}"
            + (recipe is null ? "" : $"&recipe={Uri.EscapeDataString(recipe)}"),
            "snapshot",
            cancellationToken);

    public Task<IReadOnlyList<ItemResponse>> GetItemsAsync(CancellationToken cancellationToken) =>
        GetListAsync<ItemResponse>("/api/v1/catalog/items", cancellationToken);

    public Task<IReadOnlyList<BuildableResponse>> GetBuildablesAsync(CancellationToken cancellationToken) =>
        GetListAsync<BuildableResponse>("/api/v1/catalog/buildables", cancellationToken);

    /// <param name="produces">Only the recipes that make this item (its class name); every recipe when null.</param>
    public Task<IReadOnlyList<RecipeResponse>> GetRecipesAsync(string? produces, CancellationToken cancellationToken) =>
        GetListAsync<RecipeResponse>(
            produces is null ? "/api/v1/catalog/recipes" : $"/api/v1/catalog/recipes?produces={Uri.EscapeDataString(produces)}", cancellationToken);

    public Task<IReadOnlyList<TransportTierResponse>> GetTransportTiersAsync(CancellationToken cancellationToken) =>
        GetListAsync<TransportTierResponse>("/api/v1/catalog/transport-tiers", cancellationToken);

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string path, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<T>>(JsonSerializerOptions.Web, cancellationToken) ?? [];
    }

    /// <summary>Findings, once the analysis service has made them: 202 means it's still at work (ADR-0018).</summary>
    private async Task<T> GetAnalysedAsync<T>(string path, string what, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            throw new McpException($"This {what} is still being analysed. Ask again in a little while.");
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new McpException($"No such {what}.");
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonSerializerOptions.Web, cancellationToken)
            ?? throw new McpException($"The ledger gave no findings for this {what}.");
    }
}
