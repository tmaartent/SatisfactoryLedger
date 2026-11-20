namespace SatisfactoryLedger.Mcp;

/// <summary>
/// The MCP server's entry point. A class of its own rather than top-level statements, as the analysis service has: those
/// would make another public <c>Program</c>.
/// </summary>
internal static class Program
{
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.AddLedgerMcp();
        WebApplication app = builder.Build();
        app.MapLedgerMcp();
        await app.RunAsync();
    }
}
