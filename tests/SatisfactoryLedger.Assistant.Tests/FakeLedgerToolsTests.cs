using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol.Client;

namespace SatisfactoryLedger.Assistant.Tests;

/// <summary>The fake ledger tools stand in for the real MCP server's (step 99): by name, and by what they take.</summary>
public class FakeLedgerToolsTests
{
    [Fact]
    public async Task Each_fake_tool_takes_what_the_real_tool_of_its_name_takes()
    {
        IList<McpClientTool> real = await RealLedgerTools.ListAsync();
        IList<McpClientTool> fakes = await McpTools.ListAsync(
            builder =>
            {
                builder.Services.AddSingleton(new FakeLedger());
                builder.Services.AddMcpServer().WithHttpTransport().WithTools<FakeLedgerTools>();
            },
            app => app.MapMcp("/mcp"));

        Assert.All(fakes, fake =>
        {
            McpClientTool? original = real.SingleOrDefault(tool => tool.Name == fake.Name);
            Assert.True(original is not null, $"The MCP server has no tool {fake.Name}.");
            Assert.Equal(Parameters(original!.JsonSchema), Parameters(fake.JsonSchema));
        });
    }

    /// <summary>A tool's parameters: each name with its type, and which are required.</summary>
    private static string Parameters(JsonElement schema)
    {
        IEnumerable<string> each = schema.GetProperty("properties").EnumerateObject()
            .Select(property => $"{property.Name}: {property.Value.GetProperty("type")}")
            .Order(StringComparer.Ordinal);
        IEnumerable<string> required = schema.TryGetProperty("required", out JsonElement names)
            ? names.EnumerateArray().Select(name => name.GetString()!).Order(StringComparer.Ordinal)
            : [];
        return string.Join(", ", each) + " | required: " + string.Join(", ", required);
    }
}
