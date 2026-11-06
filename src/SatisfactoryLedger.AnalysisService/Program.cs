namespace SatisfactoryLedger.AnalysisService;

/// <summary>
/// The service's entry point. A class of its own rather than top-level statements: those would make a second public
/// <c>Program</c> next to the API's, and the integration tests reference both.
/// </summary>
internal static class Program
{
    public static async Task Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.AddAnalysisService();
        await builder.Build().RunAsync();
    }
}
