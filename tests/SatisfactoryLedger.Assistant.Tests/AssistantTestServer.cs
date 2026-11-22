using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SatisfactoryLedger.Assistant.Models;

namespace SatisfactoryLedger.Assistant.Tests;

/// <summary>
/// The Assistant in memory, as the AppHost runs it, but with a fake MCP server for the ledger's tools (a real one, over
/// HTTP, answering what a <see cref="FakeLedger"/> holds) and a fake chat model as its only model.
/// </summary>
internal sealed class AssistantTestServer : IAsyncDisposable
{
    private readonly WebApplication _ledgerTools;
    private readonly WebApplication _assistant;

    private AssistantTestServer(WebApplication ledgerTools, WebApplication assistant)
    {
        _ledgerTools = ledgerTools;
        _assistant = assistant;
        Client = assistant.GetTestClient();
    }

    public HttpClient Client { get; }

    public static async Task<AssistantTestServer> StartAsync(FakeLedger ledger, IChatClient chat)
    {
        WebApplicationBuilder toolsBuilder = WebApplication.CreateBuilder();
        toolsBuilder.WebHost.UseTestServer();
        toolsBuilder.Services.AddSingleton(ledger);
        toolsBuilder.Services.AddMcpServer().WithHttpTransport().WithTools<FakeLedgerTools>();
        WebApplication tools = toolsBuilder.Build();
        tools.MapMcp("/mcp");
        await tools.StartAsync(TestContext.Current.CancellationToken);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        // Only the handler is swapped: the address stays the one the AppHost resolves, so the MCP client gets it as is.
        builder.AddLedgerAssistant(mcp => mcp
            .ConfigurePrimaryHttpMessageHandler(() => tools.GetTestServer().CreateHandler()));
        builder.Services.AddKeyedSingleton(ChatModels.Local, chat);
        builder.Services.AddSingleton(new ChatModels([ChatModels.Local]));
        WebApplication assistant = builder.Build();
        assistant.MapLedgerAssistant();
        await assistant.StartAsync(TestContext.Current.CancellationToken);

        return new AssistantTestServer(tools, assistant);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _assistant.DisposeAsync();
        await _ledgerTools.DisposeAsync();
    }
}
