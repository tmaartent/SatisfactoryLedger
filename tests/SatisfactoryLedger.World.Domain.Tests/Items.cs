namespace SatisfactoryLedger.World.Domain.Tests;

internal static class Items
{
    public static readonly ItemId IronOre = ItemId.Of("Desc_OreIron_C");
    public static readonly ItemId IronIngot = ItemId.Of("Desc_IronIngot_C");
    public static readonly ItemId IronPlate = ItemId.Of("Desc_IronPlate_C");
    public static readonly ItemId IronRod = ItemId.Of("Desc_IronRod_C");
    public static readonly ItemId Screw = ItemId.Of("Desc_IronScrew_C");
    public static readonly ItemId ReinforcedIronPlate = ItemId.Of("Desc_IronPlateReinforced_C");
    public static readonly ItemId Coal = ItemId.Of("Desc_Coal_C");
    public static readonly ItemId UraniumFuelRod = ItemId.Of("Desc_NuclearFuelRod_C");
    public static readonly ItemId UraniumWaste = ItemId.Of("Desc_NuclearWaste_C");
    public static readonly ItemId Water = ItemId.Of("Desc_Water_C");
    public static readonly ItemId NitrogenGas = ItemId.Of("Desc_NitrogenGas_C");
    public static readonly ItemId PackagedNitrogenGas = ItemId.Of("Desc_PackagedNitrogenGas_C");
    public static readonly ItemId EmptyFluidTank = ItemId.Of("Desc_GasTank_C");
    public static readonly ItemId CoolingSystem = ItemId.Of("Desc_CoolingSystem_C");

    /// <summary>The fluids among these items: pipes carry them, belts carry the rest.</summary>
    public static readonly IReadOnlySet<ItemId> Fluids = new HashSet<ItemId> { Water, NitrogenGas };
}