using System.Text.Json.Serialization;

namespace SatisfactoryLedger.Catalog.Importer;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "NativeClass")]
[JsonDerivedType(typeof(ResourceItemGroupDto), typeDiscriminator: "/Script/CoreUObject.Class'/Script/FactoryGame.FGResourceDescriptor'")]
[JsonDerivedType(typeof(ItemGroupDto), typeDiscriminator: "/Script/CoreUObject.Class'/Script/FactoryGame.FGItemDescriptor'")]
[JsonDerivedType(typeof(BiomassItemGroupDto), typeDiscriminator: "/Script/CoreUObject.Class'/Script/FactoryGame.FGItemDescriptorBiomass'")]
[JsonDerivedType(typeof(AmmoTypeProjectileItemGroupDto), typeDiscriminator: "/Script/CoreUObject.Class'/Script/FactoryGame.FGAmmoTypeProjectile'")]
[JsonDerivedType(typeof(AmmoTypeInstantHitItemGroupDto), typeDiscriminator: "/Script/CoreUObject.Class'/Script/FactoryGame.FGAmmoTypeInstantHit'")]
[JsonDerivedType(typeof(AmmoTypeSpreadshotItemGroupDto), typeDiscriminator: "/Script/CoreUObject.Class'/Script/FactoryGame.FGAmmoTypeSpreadshot'")]
[JsonDerivedType(typeof(PowerShardItemGroupDto), typeDiscriminator: "/Script/CoreUObject.Class'/Script/FactoryGame.FGPowerShardDescriptor'")]
[JsonDerivedType(typeof(NuclearFuelItemGroupDto), typeDiscriminator: "/Script/CoreUObject.Class'/Script/FactoryGame.FGItemDescriptorNuclearFuel'")]
[JsonDerivedType(typeof(PowerBoosterFuelItemGroupDto), typeDiscriminator: "/Script/CoreUObject.Class'/Script/FactoryGame.FGItemDescriptorPowerBoosterFuel'")]
[JsonDerivedType(typeof(EquipmentItemGroupDto), typeDiscriminator: "/Script/CoreUObject.Class'/Script/FactoryGame.FGEquipmentDescriptor'")]
[JsonDerivedType(typeof(RecipeGroupDto), typeDiscriminator: "/Script/CoreUObject.Class'/Script/FactoryGame.FGRecipe'")]
[JsonDerivedType(typeof(BuildingGroupDto), typeDiscriminator: "/Script/CoreUObject.Class'/Script/FactoryGame.FGBuildableManufacturer'")]
[JsonDerivedType(typeof(VariablePowerBuildingGroupDto), typeDiscriminator: "/Script/CoreUObject.Class'/Script/FactoryGame.FGBuildableManufacturerVariablePower'")]
[JsonDerivedType(typeof(BeltGroupDto), typeDiscriminator: "/Script/CoreUObject.Class'/Script/FactoryGame.FGBuildableConveyorBelt'")]
[JsonDerivedType(typeof(PipeGroupDto), typeDiscriminator: "/Script/CoreUObject.Class'/Script/FactoryGame.FGBuildablePipeline'")]
internal class GameDataGroupDto
{
    
}