using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

using SatisfactoryLedger.Api.IntegrationTests.Catalog;
using SatisfactoryLedger.Api.IntegrationTests.World;
using SatisfactoryLedger.Mcp;

namespace SatisfactoryLedger.Api.IntegrationTests.Mcp;

/// <summary>
/// The MCP server against the real API (step 99): the test world imported and analysed, and its tools called as a
/// model would. The MCP's own tests answer it with a fake API; these check that its reading of the real one holds.
/// </summary>
public sealed class LedgerMcpApiTests(LedgerApiFactory api) : IAsyncLifetime
{
    private readonly HttpClient _client = api.CreateClient();
    private WebApplication? _mcp;
    private McpClient? _model;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        await _client.ImportAsync(CatalogClient.GameFile);
        await _client.ImportResourceNodesAsync();
        Snapshot snapshot = await _client.ImportSaveAsync(WorldClient.TestWorld);
        await _client.WaitForAnalysisAsync(snapshot.Id);

        // The MCP server in memory, as the AppHost runs it, its ledger client pointed at the API in memory.
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddLedgerMcp(ledger => ledger
            .ConfigurePrimaryHttpMessageHandler(() => api.Server.CreateHandler())
            .ConfigureHttpClient(client => client.BaseAddress = api.Server.BaseAddress));
        _mcp = builder.Build();
        _mcp.MapLedgerMcp();
        await _mcp.StartAsync(TestContext.Current.CancellationToken);

        HttpClient http = _mcp.GetTestClient();
        var transport = new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "/mcp") }, http);
        _model = await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_model is not null)
        {
            await _model.DisposeAsync();
        }

        if (_mcp is not null)
        {
            await _mcp.DisposeAsync();
        }
    }

    [Fact]
    public async Task Lists_the_imported_world_as_analysed()
    {
        JsonElement snapshots = await CallAsync("list_snapshots");

        JsonElement only = Assert.Single(snapshots.EnumerateArray());
        Assert.Equal("Development", only.GetProperty("world").GetString());
        Assert.Equal("ready", only.GetProperty("analysis").GetString());
    }

    [Fact]
    public async Task Sums_up_the_findings_as_the_api_has_them()
    {
        JsonElement machines = (await CallAsync("get_findings_summary")).GetProperty("machines");

        Assert.Equal(5, machines.GetProperty("total").GetInt32());
        Assert.Equal(1, machines.GetProperty("starved").GetInt32());
    }

    [Fact]
    public async Task Traces_the_starved_rod_constructor_in_in_game_names()
    {
        JsonElement trace = await CallAsync("trace_upstream", new() { ["building"] = "Constructor", ["recipe"] = "Iron Rod" });

        Assert.Equal(1, trace.GetProperty("traced").GetInt32());
        JsonElement start = Assert.Single(trace.GetProperty("starts").EnumerateArray());
        Assert.Equal("Iron Ingot", start.GetProperty("item").GetString());
        Assert.False(string.IsNullOrEmpty(start.GetProperty("causeTerm").GetString()));
    }

    /// <summary>Calls a tool and reads its answer as JSON; fails the test when the tool reports an error.</summary>
    private async Task<JsonElement> CallAsync(string tool, Dictionary<string, object?>? arguments = null)
    {
        CallToolResult result = await _model!.CallToolAsync(tool, arguments, cancellationToken: TestContext.Current.CancellationToken);
        string text = string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        Assert.False(result.IsError ?? false, text);
        return JsonDocument.Parse(text).RootElement.Clone();
    }
}
