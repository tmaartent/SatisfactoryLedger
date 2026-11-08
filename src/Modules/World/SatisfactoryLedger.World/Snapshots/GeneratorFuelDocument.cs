namespace SatisfactoryLedger.World.Snapshots;

/// <param name="EnergyMegajoules">Per item, or per cubic metre of a fluid fuel.</param>
internal sealed record GeneratorFuelDocument(string Fuel, decimal EnergyMegajoules, string? Supplemental, string? Byproduct, decimal ByproductPerFuel);