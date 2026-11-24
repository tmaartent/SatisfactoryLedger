var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL in a container (ADR-0007). The data volume keeps the database between runs.
var ledger = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("ledger");

// RabbitMQ carries SnapshotImported from the API to the analysis service (ADR-0018). The management UI shows the queues.
var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

var api = builder.AddProject<Projects.SatisfactoryLedger_Api>("api")
    .WithReference(ledger)
    .WithReference(rabbitmq)
    .WaitFor(ledger)
    .WaitFor(rabbitmq);

// The analysis service finds the API as "api" by service discovery, and owns no database.
builder.AddProject<Projects.SatisfactoryLedger_AnalysisService>("analysis")
    .WithReference(rabbitmq)
    .WithReference(api)
    .WaitFor(rabbitmq)
    .WaitFor(api);

// The MCP server (AI enhancement 1): a model investigates a world through it, at http://localhost:5280/mcp. It reads
// the API by service discovery, as any client would, and only reads.
var mcp = builder.AddProject<Projects.SatisfactoryLedger_Mcp>("mcp")
    .WithReference(api)
    .WaitFor(api);

// The local chat model (ADR-0030): Ollama in a container, on the GPU. The data volume keeps the model between runs;
// the first run downloads it (some 4.7 GB), and the Assistant waits for that. The Ollama connection string names the model.
// Ollama's context is 4k tokens below 24 GB of VRAM, and a longer prompt silently loses its start (the rules): 16k fits a
// large trace, and its cache (about 1 GB for qwen2.5:7b) still fits the 8 GB GPU next to the model.
var chat = builder.AddOllama("ollama")
    .WithDataVolume()
    .WithGPUSupport()
    .WithEnvironment("OLLAMA_CONTEXT_LENGTH", "16384")
    .AddModel("chat", "qwen2.5:7b");

// The Assistant (AI enhancement 3) explains a finding: facts from the MCP server's tools, words from a chat model.
var assistant = builder.AddProject<Projects.SatisfactoryLedger_Assistant>("assistant")
    .WithReference(mcp)
    .WithReference(chat)
    .WaitFor(mcp)
    .WaitFor(chat);

// Claude as a second model, only with an API key: dotnet user-secrets set Parameters:anthropic-api-key <key>
// --project src/SatisfactoryLedger.AppHost. Without one, the Assistant has the local model alone.
if (!string.IsNullOrEmpty(builder.Configuration["Parameters:anthropic-api-key"]))
{
    var anthropicApiKey = builder.AddParameter("anthropic-api-key", secret: true);
    assistant.WithEnvironment("Anthropic__ApiKey", anthropicApiKey);
}

// The Angular dev server (ADR-0019). Its proxy sends /api to the API, whose address WithReference passes on as
// services__api__https__0. "start" first generates the API client from api/openapi.json.
// It also reaches the Assistant (services__assistant__http__0), without waiting for it: a first model download
// shouldn't hold up the app, only its explanations.
// It listens on every network the PC is on, not only on localhost, so a phone or tablet on the same network can open it
// at the PC's address (asked 2026-11-24, ADR-0033); it alone needs to, as it passes /api on to the API itself. The "--"
// is npm's: Aspire runs `npm run start <args>` without it, and npm would take --host as its own setting (tried 2026-11-24).
builder.AddJavaScriptApp("web", "../../web", "start")
    .WithRunScript("start", ["--", "--host", "0.0.0.0"])
    .WithReference(api)
    .WithReference(assistant)
    .WaitFor(api)
    .WithHttpEndpoint(port: 4200, isProxied: false)
    .WithExternalHttpEndpoints();

builder.Build().Run();
