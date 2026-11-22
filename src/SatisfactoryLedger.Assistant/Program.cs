using SatisfactoryLedger.Assistant.Models;

namespace SatisfactoryLedger.Assistant;

/// <summary>
/// The Assistant's entry point. A class of its own rather than top-level statements, as the MCP server has: those would
/// make another public <c>Program</c>.
/// </summary>
internal static class Program
{
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.AddLedgerAssistant();
        builder.AddChatModels();
        WebApplication app = builder.Build();
        app.MapLedgerAssistant();
        await app.RunAsync();
    }
}
