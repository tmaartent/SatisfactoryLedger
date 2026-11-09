namespace SatisfactoryLedger.Catalog.Domain.GameObjects;

public sealed record GameData(
        List<Item> Items, List<Building> Buildings, List<TransportTier> TransportTiers, List<Recipe> Recipes, List<Extractor> Extractors,
        List<Footprint> Footprints, List<Generator> Generators, List<DronePort> DronePorts);