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

// The Angular dev server (ADR-0019). Its proxy sends /api to the API, whose address WithReference passes on as
// services__api__https__0. "start" first generates the API client from api/openapi.json.
builder.AddJavaScriptApp("web", "../../web", "start")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(port: 4200, isProxied: false)
    .WithExternalHttpEndpoints();

builder.Build().Run();
