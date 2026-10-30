namespace SatisfactoryLedger.Catalog.Domain.Tests;

public static class TestCatalog
{
    internal static class Items
    {
        public static Item IronOre => Item.Create(ItemId.Of("Desc_OreIron_C"), "Iron Ore", ItemForm.Solid, true);

        public static Item IronIngot => Item.Create(ItemId.Of("Desc_IronIngot_C"), "Iron Ingots", ItemForm.Solid, false);

        public static Item IronPlate => Item.Create(ItemId.Of("Desc_IronPlate_C"), "Iron Plate", ItemForm.Solid, false);
    
        public static Item Wire => Item.Create(ItemId.Of("Desc_Wire_C"), "Iron Wire", ItemForm.Solid, false);
    
        public static Item ReinforcedIronPlate => Item.Create(ItemId.Of("Desc_IronPlateReinforced_C"), "Stitched Iron Plate", ItemForm.Solid, false);
    }
}