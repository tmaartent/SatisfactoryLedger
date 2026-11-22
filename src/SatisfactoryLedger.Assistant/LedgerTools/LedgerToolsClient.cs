using System.Text.Json;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace SatisfactoryLedger.Assistant.LedgerTools;

/// <summary>
/// The ledger's tools, through its MCP server: the Assistant is an MCP client, as Claude Code is (ADR-0030). Code
/// chooses the tool here, not the model. A typed HttpClient, so service discovery finds the server.
/// </summary>
internal sealed class LedgerToolsClient(HttpClient http, ILoggerFactory loggerFactory)
{
    /// <summary>Calls one tool. What it reports as an error becomes a <see cref="LedgerToolException"/> with its message.</summary>
    public async Task<ToolCall> CallAsync(string tool, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        var transport = new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "/mcp") }, http, loggerFactory);
        await using McpClient client = await McpClient.CreateAsync(transport, loggerFactory: loggerFactory, cancellationToken: cancellationToken);

        // The tool's description says what its answer means (every cause, for a trace): the model gets it as is.
        IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
        McpClientTool described = tools.FirstOrDefault(listed => listed.Name == tool)
            ?? throw new LedgerToolException($"The ledger's MCP server has no tool {tool}.");

        CallToolResult result = await client.CallToolAsync(tool, arguments, cancellationToken: cancellationToken);
        string text = string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        return result.IsError == true
            ? throw new LedgerToolException(text)
            : new ToolCall(tool, described.Description, arguments, JsonDocument.Parse(text).RootElement.Clone());
    }
}
