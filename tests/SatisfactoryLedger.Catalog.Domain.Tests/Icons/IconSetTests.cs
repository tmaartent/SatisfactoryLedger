using SatisfactoryLedger.Catalog.Domain.Icons;

namespace SatisfactoryLedger.Catalog.Domain.Tests.Icons;

public class IconSetTests
{
    private static Icon Of(string className) => new(className, [1, 2, 3], "image/png");

    [Fact]
    public void An_icon_set_holds_the_icon_of_each_descriptor()
    {
        IconSet icons = IconSet.Create("1.2", [Of("Desc_IronPlate_C"), Of("Desc_ConstructorMk1_C")]);

        Assert.Equal(["Desc_IronPlate_C", "Desc_ConstructorMk1_C"], icons.Icons.Select(icon => icon.ClassName));
    }

    [Fact]
    public void An_icon_set_with_a_descriptor_twice_is_refused()
    {
        Assert.Throws<ArgumentException>(() => IconSet.Create("1.2", [Of("Desc_IronPlate_C"), Of("Desc_IronPlate_C")]));
    }

    [Fact]
    public void An_icon_without_an_image_is_refused()
    {
        Assert.Throws<ArgumentException>(() => IconSet.Create("1.2", [new Icon("Desc_IronPlate_C", [], "image/png")]));
    }

    [Fact]
    public void An_icon_set_without_a_game_version_is_refused()
    {
        Assert.Throws<ArgumentException>(() => IconSet.Create(" ", [Of("Desc_IronPlate_C")]));
    }

    [Fact]
    public void An_item_s_icon_is_its_own_descriptor_s()
    {
        Assert.Equal("Desc_IronPlate_C", IconSet.DescriptorOf("Desc_IronPlate_C"));
    }

    [Fact]
    public void A_building_s_icon_is_the_descriptor_it_is_built_from()
    {
        Assert.Equal("Desc_ConstructorMk1_C", IconSet.DescriptorOf("Build_ConstructorMk1_C"));
    }
}
