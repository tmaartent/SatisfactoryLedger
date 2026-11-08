namespace SatisfactoryLedger.World.Factories;

internal sealed record DefineFactoryRequest(string? World, string? Name, string? OutputBelt, string[]? InputItems);