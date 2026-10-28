namespace SatisfactoryLedger.Catalog.Domain.Tests;

public static class TestCatalog
{
    internal static class Items
    {
        public static Item IronOre => Item.Create(ItemId.Of("Desc_IronOre_C"), "Iron Ore", ItemForm.Solid, true);

        public static Item IronIngot => Item.Create(ItemId.Of("Desc_IronIngot_C"), "Iron Ingots", ItemForm.Solid, false);

        public static Item IronPlate => Item.Create(ItemId.Of("Desc_IronPlate_C"), "Iron Plate", ItemForm.Solid, false);
    
        public static Item IronWire => Item.Create(ItemId.Of("Desc_IronWire_C"), "Iron Wire", ItemForm.Solid, false);
    
        public static Item StitchedIronPlate => Item.Create(ItemId.Of("Desc_StitchedIronPlate_C"), "Stitched Iron Plate", ItemForm.Solid, false);
    }
}