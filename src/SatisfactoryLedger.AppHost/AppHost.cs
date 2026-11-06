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

builder.Build().Run();
