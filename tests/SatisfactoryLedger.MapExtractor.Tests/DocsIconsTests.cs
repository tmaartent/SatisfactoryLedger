namespace SatisfactoryLedger.MapExtractor.Tests;

/// <summary>Which texture is each descriptor's icon, as the game's Docs file names them (ADR-0032).</summary>
public class DocsIconsTests
{
    private const string Docs = """
        [
          { "NativeClass": "/Script/CoreUObject.Class'/Script/FactoryGame.FGItemDescriptor'", "Classes": [
            { "ClassName": "Desc_IronPlate_C", "mPersistentBigIcon": "Texture2D /Game/FactoryGame/Resource/Parts/IronPlate/UI/IconDesc_IronPlates_256.IconDesc_IronPlates_256" },
            { "ClassName": "Desc_NoIcon_C", "mPersistentBigIcon": "None" }
          ] },
          { "NativeClass": "/Script/CoreUObject.Class'/Script/FactoryGame.FGBuildableManufacturer'", "Classes": [
            { "ClassName": "Build_ConstructorMk1_C", "mDisplayName": "Constructor" }
          ] }
        ]
        """;

    [Fact]
    public void A_descriptor_s_icon_is_the_texture_its_big_icon_names_in_the_game_s_files()
    {
        IReadOnlyDictionary<string, string> icons = DocsIcons.Read(Docs);

        Assert.Equal(
            "FactoryGame/Content/FactoryGame/Resource/Parts/IronPlate/UI/IconDesc_IronPlates_256.IconDesc_IronPlates_256",
            icons["Desc_IronPlate_C"]);
    }

    [Fact]
    public void A_class_with_no_icon_is_left_out()
    {
        Assert.Equal(["Desc_IronPlate_C"], DocsIcons.Read(Docs).Keys);
    }
}
