namespace SatisfactoryLedger.Api.IntegrationTests.World;

public sealed record FactoryTrace(
    Guid FactoryId, string Name, bool Traceable, Machine[] Machines, int Logistics, Connection[] Outputs, Connection[] Inputs, Reference[] Missing);