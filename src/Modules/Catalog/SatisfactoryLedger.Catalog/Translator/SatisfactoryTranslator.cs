using System.Globalization;

using SatisfactoryLedger.Catalog.Domain;
using SatisfactoryLedger.Catalog.Importer;

namespace SatisfactoryLedger.Catalog.Translator;

internal class SatisfactoryTranslator
{
    public static List<Item> TranslateToItems(List<GameDataGroupDto> dataList)
    {
        var itemGroupDto = dataList.OfType<ItemGroupDto>().ToList();
        
        List<Item> items = [];

        itemGroupDto.ForEach(dto => items.AddRange(dto.Items.Select(itemDto =>
            {
                ItemForm itemForm = FormConverter.ToItemForm(itemDto.Form);
                return Item.Create(
                    ItemId.Of(itemDto.ClassName),
                    itemDto.DisplayName,
                    itemForm,
                    dto is ResourceItemGroupDto
                );
            })
        ));

        return items;
    }

    public static List<Building> TranslateToBuildings(List<GameDataGroupDto> dataList)
    {
        var buildingGroup = dataList.OfType<BuildingGroupDto>().ToList();
        List<BuildingDto> buildingDtos = [.. buildingGroup.SelectMany(dto => dto.Buildings)];

        return
        [
            .. buildingDtos.Select(dto =>
                Building.Create(
                    BuildingId.Of(dto.ClassName),
                    dto.DisplayName,
                    int.Parse(dto.ProductionShardSlotSize, CultureInfo.InvariantCulture)
                )
            )
        ];
    }

    public static List<TransportTier> TranslateToTransportTiers(List<GameDataGroupDto> dataList)
    {
        var beltDtos = dataList.OfType<BeltGroupDto>().SelectMany(dto => dto.Tiers);
        var liftDtos = dataList.OfType<LiftGroupDto>().SelectMany(dto => dto.Tiers);
        var pipeDtos = dataList.OfType<PipeGroupDto>().SelectMany(dto => dto.Tiers);

        // A lift carries solids at the speed of the belt of the same mark, so it's a belt-form tier.
        return
        [
            .. beltDtos.Select(CreateBeltTransportTier),
            .. liftDtos.Select(CreateBeltTransportTier),
            .. pipeDtos.Select(CreatePipeTransportTier)
        ];
    }

    private static TransportTier CreatePipeTransportTier(PipeDto pipeDto)
    {
        return TransportTier.Create(
            TransportTierId.Of(pipeDto.ClassName),
            pipeDto.DisplayName,
            ConvertFlowLimit(pipeDto.FlowLimit),
            TransportForm.Pipe
        );
    }

    private static TransportTier CreateBeltTransportTier(BeltDto beltDto)
    {
        return TransportTier.Create(
            TransportTierId.Of(beltDto.ClassName),
            beltDto.DisplayName,
            ConvertSpeed(beltDto.Speed),
            TransportForm.Belt
        );
    }

    public static List<Recipe> TranslateToRecipes(List<GameDataGroupDto> dataList)
    {
        var recipeGroupDtos = dataList.OfType<RecipeGroupDto>().ToList();
        List<RecipeDto> recipeDtos = [.. recipeGroupDtos.SelectMany(dto => dto.Recipes)];
        var itemGroupDtos = dataList.OfType<ItemGroupDto>().ToList();
        Dictionary<string, ItemDto> itemLookup = itemGroupDtos.SelectMany(dto => dto.Items)
            .ToDictionary(dto => dto.ClassName, dto => dto);

        return
        [
            .. recipeDtos.Select(recipeDto =>
                Recipe.Create(
                    RecipeId.Of(recipeDto.ClassName),
                    recipeDto.DisplayName,
                    BuildingId.Of(recipeDto.RecipeBuilding!.ClassName),
                    ParseGameDecimal(recipeDto.ManufactoringDuration),
                    [
                        .. recipeDto.Ingredients.Select(ingredientDto =>
                            itemLookup.TryGetValue(ingredientDto.ClassName, out var item)
                                ? RecipeIngredient.Of(
                                    ItemId.Of(ingredientDto.ClassName),
                                    item.IsFluid ? (decimal)ingredientDto.Amount / 1000 : ingredientDto.Amount
                                )
                                : throw new KeyNotFoundException($"Key {ingredientDto.ClassName} not found"))
                    ],
                    [
                        .. recipeDto.Products.Select(productDto =>
                            itemLookup.TryGetValue(productDto.ClassName, out var item)
                                ? RecipeIngredient.Of(
                                    ItemId.Of(productDto.ClassName),
                                    item.IsFluid ? (decimal)productDto.Amount / 1000 : productDto.Amount
                                )
                                : throw new KeyNotFoundException($"Key {productDto.ClassName} not found"))
                    ],
                    recipeDto.ClassName.Contains("Alternate", StringComparison.InvariantCulture)
                )
            )
        ];
    }

    public static List<Extractor> TranslateToExtractors(List<GameDataGroupDto> dataList)
    {
        return
        [
            .. dataList.OfType<ExtractorGroupDto>().SelectMany(dto => dto.Extractors).Select(dto =>
            {
                // "(RF_LIQUID,RF_GAS)": a resource well extractor takes either; both are fluids.
                string firstForm = dto.AllowedResourceForms.Trim('(', ')').Split(',')[0];
                ItemForm form = FormConverter.ToItemForm(firstForm);
                decimal itemsPerCycle = ParseGameDecimal(dto.ItemsPerCycle);

                return Extractor.Create(
                    ExtractorId.Of(dto.ClassName),
                    dto.DisplayName,
                    form,
                    form == ItemForm.Fluid ? itemsPerCycle / 1000 : itemsPerCycle,
                    ParseGameDecimal(dto.ExtractCycleTime));
            })
        ];
    }

    /// <summary>
    /// Generators with the fuels they burn (BR-A8). A fuel's energy is the item's <c>mEnergyValue</c>: per item, or per
    /// litre of a fluid, so ×1000 for a cubic metre. A fuel the file has no item or no energy for is left out.
    /// </summary>
    public static List<Generator> TranslateToGenerators(List<GameDataGroupDto> dataList)
    {
        Dictionary<string, ItemDto> items = dataList.OfType<ItemGroupDto>().SelectMany(dto => dto.Items)
            .GroupBy(item => item.ClassName, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        static ItemId? ItemOrNone(string className) => string.IsNullOrWhiteSpace(className) ? null : ItemId.Of(className);

        return
        [
            .. dataList.OfType<GeneratorGroupDto>().SelectMany(dto => dto.Generators).Select(dto => Generator.Create(
                GeneratorId.Of(dto.ClassName),
                dto.DisplayName,
                ParseGameDecimal(dto.PowerProduction),
                ParseGameDecimal(dto.SupplementalToPowerRatio),
                [
                    .. dto.Fuels
                        .Where(fuel => items.TryGetValue(fuel.FuelClass, out ItemDto? item) && ParseGameDecimal(item.EnergyValue) > 0)
                        .Select(fuel =>
                        {
                            ItemDto item = items[fuel.FuelClass];
                            decimal energy = ParseGameDecimal(item.EnergyValue) * (item.IsFluid ? 1000 : 1);
                            return new GeneratorFuel(
                                ItemId.Of(fuel.FuelClass),
                                energy,
                                ItemOrNone(fuel.SupplementalResourceClass),
                                ItemOrNone(fuel.Byproduct),
                                string.IsNullOrWhiteSpace(fuel.ByproductAmount) ? 0 : ParseGameDecimal(fuel.ByproductAmount));
                        })
                ]))
        ];
    }

    /// <summary>
    /// The drone port with what a trip costs (BR-A16). A drone "can use any fuel type": every solid item with energy is
    /// a fuel it can burn, at its <c>mEnergyValue</c> per item.
    /// </summary>
    public static List<DronePort> TranslateToDronePorts(List<GameDataGroupDto> dataList)
    {
        List<DroneFuel> fuels =
        [
            .. dataList.OfType<ItemGroupDto>().SelectMany(dto => dto.Items)
                .DistinctBy(item => item.ClassName, StringComparer.Ordinal)
                .Where(item => !item.IsFluid && ParseGameDecimal(item.EnergyValue) > 0)
                .OrderBy(item => item.ClassName, StringComparer.Ordinal)
                .Select(item => new DroneFuel(ItemId.Of(item.ClassName), ParseGameDecimal(item.EnergyValue)))
        ];

        return
        [
            .. dataList.OfType<DronePortGroupDto>().SelectMany(dto => dto.DronePorts).Select(dto => DronePort.Create(
                DronePortId.Of(dto.ClassName),
                dto.DisplayName,
                ParseGameDecimal(dto.TripPowerCost),
                ParseGameDecimal(dto.TripPowerPerMeterCost),
                fuels))
        ];
    }

    /// <summary>
    /// The footprint of every building with clearance boxes (ADR-0020). A class can be in more than one section; the
    /// first is taken.
    /// </summary>
    public static List<Footprint> TranslateToFootprints(List<GameDataGroupDto> dataList)
    {
        return
        [
            .. dataList.OfType<FootprintGroupDto>().SelectMany(dto => dto.Classes)
                .DistinctBy(dto => dto.ClassName, StringComparer.Ordinal)
                .Select(dto => (dto.ClassName, Boxes: ClearanceDataParser.Parse(dto.ClearanceData)))
                .Where(building => building.Boxes.Count > 0)
                .Select(building => Footprint.Around(FootprintId.Of(building.ClassName), building.Boxes))
        ];
    }

    // The game writes decimals as "6.000000". A C# decimal remembers those six places (its scale) and prints
    // them again, so drop the trailing zeros here: dividing by 1.000…0m with more places than any value has does that.
    private static decimal ParseGameDecimal(string value)
    {
        return decimal.Parse(value, CultureInfo.InvariantCulture) / 1.000000000000000000000000000000000m;
    }

    private static int ConvertSpeed(string argSpeed)
    {
        decimal rawSpeed = decimal.Parse(argSpeed, CultureInfo.InvariantCulture);
        return (int)rawSpeed / 2;
    }
    
    private static int ConvertFlowLimit(string argFlowLimit)
    {
        return (int)decimal.Parse(argFlowLimit, CultureInfo.InvariantCulture) * 60;
    }
}