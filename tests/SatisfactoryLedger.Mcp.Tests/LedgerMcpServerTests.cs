using ModelContextProtocol.Client;

namespace SatisfactoryLedger.Mcp.Tests;

public class LedgerMcpServerTests
{
    [Fact]
    public async Task Every_tool_only_reads()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        IList<McpClientTool> tools = await server.Client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEmpty(tools);
        Assert.All(tools, tool => Assert.True(tool.ProtocolTool.Annotations?.ReadOnlyHint, $"{tool.Name} is not marked read-only"));
    }

    [Fact]
    public async Task Offers_the_tools_for_snapshots_findings_factories_machines_traces_and_game_data()
    {
        await using var server = await LedgerMcpTestServer.StartAsync(IronWorld.Api());

        IList<McpClientTool> tools = await server.Client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            ["find_items", "find_machines", "get_factory_findings", "get_findings_summary", "get_recipes", "get_transport_tiers", "list_factories", "list_findings", "list_snapshots", "trace_downstream", "trace_upstream"],
            tools.Select(tool => tool.Name).Order(StringComparer.Ordinal));
    }
}
