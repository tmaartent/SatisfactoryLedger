using SatisfactoryLedger.Api.IntegrationTests.Catalog;

namespace SatisfactoryLedger.Api.IntegrationTests.World;

/// <summary>
/// The analysis service when it can't reach the API for a while (ADR-0018, step 99): a snapshot imported then is still
/// analysed once it can.
/// </summary>
public sealed class AnalysisServiceReachabilityTests(LedgerApiFactory api) : IAsyncLifetime
{
    private readonly HttpClient _client = api.CreateClient();

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        await _client.ImportAsync(CatalogClient.GameFile);
        await _client.ImportResourceNodesAsync();
    }

    public ValueTask DisposeAsync()
    {
        api.UnreachableCalls = 0;
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task A_call_to_the_api_that_fails_once_is_tried_again()
    {
        api.UnreachableCalls = 1;

        Snapshot snapshot = await _client.ImportSaveAsync(WorldClient.TestWorld);

        await _client.WaitForAnalysisAsync(snapshot.Id);
        Assert.Equal(0, api.UnreachableCalls);
    }

    [Fact]
    public async Task A_snapshot_imported_while_the_api_cant_be_reached_for_longer_is_still_analysed()
    {
        // Longer than the World client's own retries (a call and three more): the message itself is tried again.
        api.UnreachableCalls = 5;

        Snapshot snapshot = await _client.ImportSaveAsync(WorldClient.TestWorld);

        await _client.WaitForAnalysisAsync(snapshot.Id);
        Assert.Equal(0, api.UnreachableCalls);
    }
}
