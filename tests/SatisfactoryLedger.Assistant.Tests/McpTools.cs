using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

using ModelContextProtocol.Client;

namespace SatisfactoryLedger.Assistant.Tests;

/// <summary>The tools an MCP server in memory offers, as a model lists them (step 99).</summary>
internal static class McpTools
{
    public static async Task<IList<McpClientTool>> ListAsync(Action<WebApplicationBuilder> serve, Action<WebApplication> map)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        serve(builder);
        await using WebApplication app = builder.Build();
        map(app);
        await app.StartAsync(TestContext.Current.CancellationToken);

        HttpClient http = app.GetTestClient();
        var transport = new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "/mcp") }, http);
        await using McpClient client = await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);
        return await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);
    }
}
