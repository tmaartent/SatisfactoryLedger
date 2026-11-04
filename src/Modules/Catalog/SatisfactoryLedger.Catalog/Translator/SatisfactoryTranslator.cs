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