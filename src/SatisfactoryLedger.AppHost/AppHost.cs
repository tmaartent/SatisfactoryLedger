var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL in a container (ADR-0007). The data volume keeps the database between runs.
var ledger = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("ledger");

builder.AddProject<Projects.SatisfactoryLedger_Api>("api")
    .WithReference(ledger)
    .WaitFor(ledger);

builder.Build().Run();
