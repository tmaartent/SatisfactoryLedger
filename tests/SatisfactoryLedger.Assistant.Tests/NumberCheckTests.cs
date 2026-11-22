using System.Text.Json;

using SatisfactoryLedger.Assistant.Explaining;

namespace SatisfactoryLedger.Assistant.Tests;

public class NumberCheckTests
{
    private static JsonElement Facts(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void An_explanation_in_words_has_no_numbers_to_flag()
    {
        JsonElement facts = Facts("""{ "item": "Iron Ingot", "perMinute": 30 }""");

        Assert.Empty(NumberCheck.Unsupported("In the model, too few smelters make Iron Ingot. Build another Smelter.", facts));
    }

    [Fact]
    public void A_number_copied_from_the_facts_is_flagged_as_the_list_beside_the_text_gives_it()
    {
        JsonElement facts = Facts("""{ "machines": 21, "starts": [{ "perMinute": 1200, "at": [{ "count": 9 }] }] }""");

        Assert.Equal(["21", "9"], NumberCheck.Unsupported("With 21 machines needing it, only 9 units of Wire arrive.", facts));
    }

    [Fact]
    public void Numbers_in_the_names_the_facts_give_are_part_of_the_name()
    {
        JsonElement facts = Facts("""{ "at": [{ "building": "Conveyor Belt Mk.6" }], "recipe": "Alternate: Uranium-235" }""");

        Assert.Empty(NumberCheck.Unsupported("Lay another Conveyor Belt Mk.6 beside it for the Alternate: Uranium-235 machines.", facts));
    }

    [Fact]
    public void A_number_outside_a_name_is_flagged_even_when_a_name_holds_it()
    {
        JsonElement facts = Facts("""{ "at": [{ "building": "Conveyor Belt Mk.6" }] }""");

        Assert.Equal(["6"], NumberCheck.Unsupported("The 6 machines wait on one Conveyor Belt Mk.6.", facts));
    }
}
