using System.Text.Json;
using System.Text.Json.Serialization;

using ModelContextProtocol;
using ModelContextProtocol.Protocol;

using SatisfactoryLedger.Mcp.Catalog;
using SatisfactoryLedger.Mcp.LedgerApi;
using SatisfactoryLedger.Mcp.Tools;

namespace SatisfactoryLedger.Mcp;

/// <summary>Sets up the MCP server (AI enhancement 1). Program.cs and the tests both use it.</summary>
public static class LedgerMcp
{
    /// <summary>The API's name in the AppHost, for service discovery.</summary>
    private const string ApiServiceName = "api";

    private const string Instructions =
        "Investigates a Satisfactory world the player imported: why machines run below full speed, where belts are full, what supply goes " +
        "unused. Start with get_findings_summary, then follow up with find_machines, list_findings or get_factory_findings. Rates are per " +
        "minute, shares and utilisation in percent, positions in metres. Use the in-game names (\"Iron Plate\"); get_recipes and " +
        "get_transport_tiers give the game's numbers, so check against them rather than recalling them. The tools only read.";

    /// <summary>
    /// Answers without empty fields: a model reads every token. From the SDK's own options, which name the resolver that
    /// .NET 10 asks for before options are frozen.
    /// </summary>
    private static readonly JsonSerializerOptions AnswerJson = new(McpJsonUtilities.DefaultOptions)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <param name="configureLedger">Lets a test point the API client at a fake instead of service discovery.</param>
    public static IHostApplicationBuilder AddLedgerMcp(this IHostApplicationBuilder builder, Action<IHttpClientBuilder>? configureLedger = null)
    {
        builder.AddServiceDefaults();

        IHttpClientBuilder ledger = builder.Services.AddHttpClient<LedgerClient>(client =>
            // "https+http://": service discovery tries HTTPS first, then HTTP.
            client.BaseAddress = new Uri($"https+http://{ApiServiceName}"));
        configureLedger?.Invoke(ledger);

        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<SnapshotChoice>();
        builder.Services.AddScoped<GameDataSource>();

        builder.Services.AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation { Name = "satisfactory-ledger", Version = "1.0" };
                options.ServerInstructions = Instructions;
            })
            .WithHttpTransport()
            .WithTools<SnapshotTools>(AnswerJson)
            .WithTools<FactoryTools>(AnswerJson)
            .WithTools<MachineTools>(AnswerJson)
            .WithTools<CatalogTools>(AnswerJson);

        return builder;
    }

    /// <summary>The MCP endpoint at /mcp, and the health checks the AppHost watches.</summary>
    public static WebApplication MapLedgerMcp(this WebApplication app)
    {
        app.MapDefaultEndpoints();
        app.MapMcp("/mcp");
        return app;
    }
}
