using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.AI;

namespace SatisfactoryLedger.Assistant.Tests;

public class ExplanationTests
{
    /// <summary>Two of three iron plate constructors are starved: the one smelter makes too little, as trace_upstream answers.</summary>
    private const string ShortageTrace = """
        {
          "building": "Constructor", "recipe": "Iron Plate", "machines": 3, "traced": 2,
          "starts": [{
            "cause": "tooFewProducers", "causeTerm": "too few producers", "item": "Iron Ingot", "perMinute": 30,
            "at": [{ "building": "Smelter", "recipe": "Iron Ingot", "count": 1, "ids": ["Build_SmelterMk1_C_1"], "positionMetres": { "x": 12.5, "y": -3, "z": 0 } }],
            "path": [{ "building": "Constructor", "recipe": "Iron Plate", "machines": 2, "shortOf": "Iron Ingot", "perMinute": 30 }]
          }]
        }
        """;

    private const string BlockageTrace = """
        {
          "building": "Smelter", "recipe": "Iron Ingot", "machines": 1, "traced": 1,
          "starts": [{ "cause": "tooFewConsumers", "causeTerm": "too few consumers", "item": "Iron Ingot", "perMinute": 15, "at": [], "path": [] }]
        }
        """;

    private const string NothingHeldBack = """
        { "building": "Smelter", "recipe": "Iron Ingot", "machines": 1, "traced": 0, "starts": [], "note": "None of the 1 Smelter (Iron Ingot) machines is starved: there's nothing to trace." }
        """;

    private const string Explanation =
        "In the model, the 2 Constructors are short of Iron Ingot: the one Smelter makes 30 a minute at full speed. Build a second Smelter next to it.";

    private static readonly object StarvedConstructors = new
    {
        snapshotId = "s1", building = "Build_ConstructorMk1_C", recipe = "Recipe_IronPlate_C", limit = "starved",
    };

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static async Task<JsonElement> ExplainAsync(AssistantTestServer server, object request)
    {
        HttpResponseMessage response = await server.Client.PostAsJsonAsync("/api/v1/assistant/explanations", request, Cancellation);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Cancellation));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
    }

    [Fact]
    public async Task Explains_a_starved_finding_from_where_its_shortage_starts()
    {
        FakeLedger ledger = new FakeLedger().Answer("trace_upstream", ShortageTrace);
        await using var server = await AssistantTestServer.StartAsync(ledger, new FakeChatClient(Explanation));

        JsonElement explanation = await ExplainAsync(server, StarvedConstructors);

        Assert.Equal(Explanation, explanation.GetProperty("text").GetString());
        Assert.Equal(FakeChatClient.Model, explanation.GetProperty("model").GetString());
        Assert.Equal(("trace_upstream", "Build_ConstructorMk1_C", "Recipe_IronPlate_C", "s1"), Assert.Single(ledger.Asked));
        JsonElement facts = explanation.GetProperty("facts");
        Assert.Equal("trace_upstream", facts.GetProperty("tool").GetString());
        Assert.Equal("tooFewProducers", facts.GetProperty("answer").GetProperty("starts")[0].GetProperty("cause").GetString());
        Assert.Empty(explanation.GetProperty("unsupportedNumbers").EnumerateArray());
    }

    [Fact]
    public async Task Grounds_the_model_in_the_trace_and_in_the_tool_s_own_description()
    {
        var chat = new FakeChatClient(Explanation);
        await using var server = await AssistantTestServer.StartAsync(new FakeLedger().Answer("trace_upstream", ShortageTrace), chat);

        await ExplainAsync(server, StarvedConstructors);

        ChatMessage system = Assert.Single(chat.Messages, message => message.Role == ChatRole.System);
        Assert.Contains(FakeLedgerTools.UpstreamDescription, system.Text, StringComparison.Ordinal);
        ChatMessage question = Assert.Single(chat.Messages, message => message.Role == ChatRole.User);
        Assert.Contains("Constructor (Iron Plate)", question.Text, StringComparison.Ordinal);
        Assert.Contains("starved", question.Text, StringComparison.Ordinal);
        Assert.Contains("\"Smelter\"", question.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gives_the_model_each_cause_by_the_ledger_s_term_alone_so_it_does_not_repeat_the_code()
    {
        var chat = new FakeChatClient(Explanation);
        await using var server = await AssistantTestServer.StartAsync(new FakeLedger().Answer("trace_upstream", ShortageTrace), chat);

        JsonElement explanation = await ExplainAsync(server, StarvedConstructors);

        ChatMessage question = Assert.Single(chat.Messages, message => message.Role == ChatRole.User);
        Assert.Contains("\"cause\":\"too few producers\"", question.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("tooFewProducers", question.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("causeTerm", question.Text, StringComparison.Ordinal);
        Assert.Equal("tooFewProducers", explanation.GetProperty("facts").GetProperty("answer").GetProperty("starts")[0].GetProperty("cause").GetString());
    }

    [Fact]
    public async Task Leaves_the_machines_ids_and_positions_out_of_what_the_model_reads_but_not_out_of_the_facts_shown()
    {
        var chat = new FakeChatClient(Explanation);
        await using var server = await AssistantTestServer.StartAsync(new FakeLedger().Answer("trace_upstream", ShortageTrace), chat);

        JsonElement explanation = await ExplainAsync(server, StarvedConstructors);

        ChatMessage question = Assert.Single(chat.Messages, message => message.Role == ChatRole.User);
        Assert.DoesNotContain("Build_SmelterMk1_C_1", question.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("positionMetres", question.Text, StringComparison.Ordinal);
        Assert.Contains("\"Smelter\"", question.Text, StringComparison.Ordinal);
        JsonElement at = explanation.GetProperty("facts").GetProperty("answer").GetProperty("starts")[0].GetProperty("at")[0];
        Assert.Equal("Build_SmelterMk1_C_1", at.GetProperty("ids")[0].GetString());
        Assert.Equal(12.5, at.GetProperty("positionMetres").GetProperty("x").GetDouble());
    }

    [Fact]
    public async Task Traces_an_output_blocked_finding_downstream()
    {
        var chat = new FakeChatClient("The Smelter makes more Iron Ingot than its consumers take.");
        FakeLedger ledger = new FakeLedger().Answer("trace_downstream", BlockageTrace);
        await using var server = await AssistantTestServer.StartAsync(ledger, chat);

        JsonElement explanation = await ExplainAsync(server, new { snapshotId = "s1", building = "Build_SmelterMk1_C", limit = "outputBlocked" });

        Assert.Equal(("trace_downstream", "Build_SmelterMk1_C", null, "s1"), Assert.Single(ledger.Asked));
        Assert.Contains(FakeLedgerTools.DownstreamDescription, chat.Messages[0].Text, StringComparison.Ordinal);
        Assert.Contains("output blocked", chat.Messages[1].Text, StringComparison.Ordinal);
        Assert.Equal("trace_downstream", explanation.GetProperty("facts").GetProperty("tool").GetString());
    }

    [Fact]
    public async Task Flags_the_numbers_the_model_did_not_take_from_the_facts()
    {
        var chat = new FakeChatClient("They need 45 a minute and get 30, so they are short by 15.");
        await using var server = await AssistantTestServer.StartAsync(new FakeLedger().Answer("trace_upstream", ShortageTrace), chat);

        JsonElement explanation = await ExplainAsync(server, StarvedConstructors);

        Assert.Equal(["45", "15"], explanation.GetProperty("unsupportedNumbers").EnumerateArray().Select(number => number.GetString()));
    }

    [Fact]
    public async Task Gives_the_trace_s_note_without_asking_a_model_when_none_of_the_machines_is_held_back()
    {
        var chat = new FakeChatClient(Explanation);
        await using var server = await AssistantTestServer.StartAsync(new FakeLedger().Answer("trace_upstream", NothingHeldBack), chat);

        JsonElement explanation = await ExplainAsync(server, new { snapshotId = "s1", building = "Build_SmelterMk1_C", limit = "starved" });

        Assert.Equal(0, chat.Calls);
        Assert.StartsWith("None of the 1 Smelter", explanation.GetProperty("text").GetString(), StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, explanation.GetProperty("model").ValueKind);
    }

    [Fact]
    public async Task Asks_no_model_when_the_trace_finds_no_start_for_the_held_back_machines()
    {
        const string noStart = """{ "building": "Blender", "recipe": "Alternate: Nitro Rocket Fuel", "machines": 12, "traced": 2, "starts": [] }""";
        var chat = new FakeChatClient("They are output blocked because there are too few consumers downstream.");
        await using var server = await AssistantTestServer.StartAsync(new FakeLedger().Answer("trace_downstream", noStart), chat);

        JsonElement explanation = await ExplainAsync(server, new { snapshotId = "s1", building = "Build_Blender_C", limit = "outputBlocked" });

        Assert.Equal(0, chat.Calls);
        Assert.Equal(JsonValueKind.Null, explanation.GetProperty("model").ValueKind);
        Assert.Contains("found no start", explanation.GetProperty("text").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Passes_on_what_the_ledger_could_not_answer()
    {
        FakeLedger ledger = new FakeLedger().Fail("trace_upstream", "This snapshot is still being analysed. Ask again in a little while.");
        await using var server = await AssistantTestServer.StartAsync(ledger, new FakeChatClient(Explanation));

        HttpResponseMessage response = await server.Client.PostAsJsonAsync("/api/v1/assistant/explanations", StarvedConstructors, Cancellation);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("still being analysed", await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refuses_a_model_it_does_not_have_naming_the_ones_it_has()
    {
        await using var server = await AssistantTestServer.StartAsync(new FakeLedger().Answer("trace_upstream", ShortageTrace), new FakeChatClient(Explanation));

        HttpResponseMessage response = await server.Client.PostAsJsonAsync(
            "/api/v1/assistant/explanations",
            new { snapshotId = "s1", building = "Build_ConstructorMk1_C", limit = "starved", model = "claude" },
            Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("local", await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lists_its_models_with_the_model_each_runs()
    {
        await using var server = await AssistantTestServer.StartAsync(new FakeLedger(), new FakeChatClient(Explanation));

        JsonElement models = await server.Client.GetFromJsonAsync<JsonElement>("/api/v1/assistant/models", Cancellation);

        JsonElement model = Assert.Single(models.EnumerateArray());
        Assert.Equal(("local", FakeChatClient.Model), (model.GetProperty("key").GetString(), model.GetProperty("model").GetString()));
    }
}
