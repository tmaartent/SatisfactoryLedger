using System.Text.Json;

using SatisfactoryLedger.Assistant.Explaining;

namespace SatisfactoryLedger.Assistant.Tests;

public class NumberCheckTests
{
    private static JsonElement Facts(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Numbers_copied_from_the_facts_are_supported()
    {
        JsonElement facts = Facts("""{ "perMinute": 22.5, "shortOf": { "Iron Ingot": 76 } }""");

        Assert.Empty(NumberCheck.Unsupported("They get 22.5 a minute, 76% of what they need.", facts));
    }

    [Fact]
    public void A_number_rounded_from_a_fact_is_supported()
    {
        JsonElement facts = Facts("""{ "perMinute": 12.37, "others": [{ "perMinute": 7.5 }] }""");

        Assert.Empty(NumberCheck.Unsupported("About 12.4 a minute, of which the others take some 8.", facts));
    }

    [Fact]
    public void A_number_the_model_worked_out_itself_is_flagged()
    {
        JsonElement facts = Facts("""{ "path": [{ "perMinute": 30 }], "starts": [{ "perMinute": 22.5 }] }""");

        Assert.Equal(["7.5"], NumberCheck.Unsupported("They need 30 and get 22.5, so they are short by 7.5 a minute.", facts));
    }

    [Fact]
    public void Numbers_in_names_ids_and_positions_count_as_facts()
    {
        JsonElement facts = Facts("""{ "building": "Miner Mk.2", "ids": ["Build_MinerMk2_C_2147"], "positionMetres": { "x": -1200.5 } }""");

        Assert.Empty(NumberCheck.Unsupported("The Miner Mk.2 (Build_MinerMk2_C_2147) at x -1200.5.", facts));
    }

    [Fact]
    public void Full_speed_is_supported_without_a_fact()
    {
        Assert.Empty(NumberCheck.Unsupported("The smelters already run at 100%.", Facts("{}")));
    }
}
