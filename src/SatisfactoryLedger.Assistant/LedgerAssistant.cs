using System.Text.Json;
using System.Text.Json.Serialization;

using SatisfactoryLedger.Assistant.Explaining;
using SatisfactoryLedger.Assistant.LedgerTools;

namespace SatisfactoryLedger.Assistant;

/// <summary>Sets up the Assistant (AI enhancement 3). Program.cs and the tests both use it; the chat models are added apart.</summary>
public static class LedgerAssistant
{
    /// <summary>The MCP server's name in the AppHost, for service discovery.</summary>
    private const string McpServiceName = "mcp";

    /// <param name="configureTools">Lets a test point the MCP client at a fake server instead of service discovery.</param>
    public static IHostApplicationBuilder AddLedgerAssistant(this IHostApplicationBuilder builder, Action<IHttpClientBuilder>? configureTools = null)
    {
        builder.AddServiceDefaults();
        builder.Services.AddProblemDetails();
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

        IHttpClientBuilder tools = builder.Services.AddHttpClient<LedgerToolsClient>(client =>
            // Plain "http://", not "https+http://": the MCP client only takes an http or https endpoint, and the MCP
            // server only listens on HTTP. Service discovery still resolves the name.
            client.BaseAddress = new Uri($"http://{McpServiceName}"));
        configureTools?.Invoke(tools);

        builder.Services.AddScoped<FindingExplainer>();
        return builder;
    }

    /// <summary>The Assistant's endpoints under /api/v1/assistant, and the health checks the AppHost watches.</summary>
    public static WebApplication MapLedgerAssistant(this WebApplication app)
    {
        app.MapDefaultEndpoints();
        app.MapAssistantEndpoints();
        return app;
    }
}
