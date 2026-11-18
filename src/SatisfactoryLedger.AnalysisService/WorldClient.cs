using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using SatisfactoryLedger.World.Contracts.Analysis;
using SatisfactoryLedger.World.Contracts.Arrangement;

namespace SatisfactoryLedger.AnalysisService;

/// <summary>World's internal endpoints for the analysis service (ADR-0018), as a typed HttpClient.</summary>
public sealed class WorldClient(HttpClient http)
{
    /// <returns>Null when World has no such snapshot.</returns>
    public async Task<FlowNetworkDocument?> GetFlowNetworkAsync(Guid snapshotId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.GetAsync(new Uri($"/internal/world/snapshots/{snapshotId}/flow-network", UriKind.Relative), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return FlowNetworkDocument.FromJson(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task PutAnalysisAsync(Guid snapshotId, AnalysisResult result, CancellationToken cancellationToken)
    {
        using var content = new StringContent(JsonSerializer.Serialize(result, SimulationJson.Options), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await http.PutAsync(new Uri($"/internal/world/snapshots/{snapshotId}/analysis", UriKind.Relative), content, cancellationToken);
        EnsureStoredOrGone(response);
    }

    public async Task PutFailureAsync(Guid snapshotId, string error, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.PutAsJsonAsync(
            new Uri($"/internal/world/snapshots/{snapshotId}/analysis/failure", UriKind.Relative), new { error }, cancellationToken);
        EnsureStoredOrGone(response);
    }

    /// <returns>Null when World has no such run, or its snapshot is gone.</returns>
    public async Task<ArrangementProblemDocument?> GetArrangementProblemAsync(Guid arrangementId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.GetAsync(new Uri($"/internal/world/arrangements/{arrangementId}/problem", UriKind.Relative), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return ArrangementProblemDocument.FromJson(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task PutArrangementAsync(Guid arrangementId, ArrangementProposalDocument proposal, CancellationToken cancellationToken)
    {
        using var content = new StringContent(proposal.ToJson(), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await http.PutAsync(new Uri($"/internal/world/arrangements/{arrangementId}/proposal", UriKind.Relative), content, cancellationToken);
        EnsureStoredOrGone(response);
    }

    public async Task PutArrangementFailureAsync(Guid arrangementId, string error, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.PutAsJsonAsync(
            new Uri($"/internal/world/arrangements/{arrangementId}/failure", UriKind.Relative), new { error }, cancellationToken);
        EnsureStoredOrGone(response);
    }

    /// <summary>404 means the snapshot was deleted meanwhile: there's nothing to store it for.</summary>
    private static void EnsureStoredOrGone(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }
    }
}