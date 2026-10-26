var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.SatisfactoryLedger_Api>("api");

builder.Build().Run();
