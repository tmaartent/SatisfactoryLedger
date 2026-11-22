using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace SatisfactoryLedger.Mcp.Tests;

/// <summary>
/// The MCP server in memory, as the AppHost runs it, but with its API answered by a <see cref="FakeLedgerApi"/>; and a
/// real MCP client talking to it over HTTP. A test calls tools as Claude Code would.
/// </summary>
internal sealed class LedgerMcpTestServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    private LedgerMcpTestServer(WebApplication app, McpClient client)
    {
        _app = app;
        Client = client;
    }

    public McpClient Client { get; }

    public static async Task<LedgerMcpTestServer> StartAsync(FakeLedgerApi api)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddLedgerMcp(ledger => ledger
            .ConfigurePrimaryHttpMessageHandler(api.Handler)
            .ConfigureHttpClient(client => client.BaseAddress = new Uri("http://api.test")));
        WebApplication app = builder.Build();
        app.MapLedgerMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        HttpClient http = app.GetTestClient();
        var transport = new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "/mcp") }, http);
        McpClient client = await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);
        return new LedgerMcpTestServer(app, client);
    }

    /// <summary>Calls a tool and reads its answer as JSON; fails the test when the tool reports an error.</summary>
    public async Task<JsonElement> CallAsync(string tool, Dictionary<string, object?>? arguments = null)
    {
        CallToolResult result = await Client.CallToolAsync(tool, arguments, cancellationToken: TestContext.Current.CancellationToken);
        string text = Text(result);
        Assert.False(result.IsError ?? false, text);
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    /// <summary>Calls a tool that should fail, and gives what it told the model.</summary>
    public async Task<string> CallFailingAsync(string tool, Dictionary<string, object?>? arguments = null)
    {
        CallToolResult result = await Client.CallToolAsync(tool, arguments, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.IsError ?? false, "The tool was expected to report an error.");
        return Text(result);
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        await _app.DisposeAsync();
    }

    private static string Text(CallToolResult result) => string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
