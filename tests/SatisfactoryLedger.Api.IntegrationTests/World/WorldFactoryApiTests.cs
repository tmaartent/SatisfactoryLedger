using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc;

using SatisfactoryLedger.Api.IntegrationTests.Catalog;

namespace SatisfactoryLedger.Api.IntegrationTests.World;

/// <summary>Factory definitions over the API: suggested output belts, defining, and tracing (BR-W2, BR-W11, BR-W12).</summary>
public sealed class WorldFactoryApiTests(LedgerApiFactory api) : IAsyncLifetime
{
    private static readonly string[] OnlyIronOre = ["Desc_OreIron_C"];

    private readonly HttpClient _client = api.CreateClient();
    private Snapshot _snapshot = null!;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        await _client.ImportAsync(CatalogClient.GameFile);
        _snapshot = await _client.ImportSaveAsync(WorldClient.TestWorld);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<Factory> DefineIronPlatesAsync()
    {
        HttpResponseMessage response = await _client.DefineFactoryAsync("Development", "Iron plates", WorldClient.BeltIntoContainer, "Desc_OreIron_C");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Factory>(TestContext.Current.CancellationToken))!;
    }

    private static async Task<ProblemDetails> ProblemAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;

    [Fact]
    public async Task A_snapshot_suggests_the_belt_into_the_container_as_an_output_belt()
    {
        var belts = await _client.GetAsync<OutputBelt[]>($"/api/v1/world/snapshots/{_snapshot.Id}/output-belts");

        OutputBelt belt = Assert.Single(belts, belt => belt.Belt == WorldClient.BeltIntoContainer);
        Assert.Equal(["Desc_IronPlate_C"], belt.Items);
        Assert.Equal(["Build_StorageContainerMk2_C"], belt.Into.Select(into => into.ClassName));
        Assert.Equal(new Dictionary<string, int> { ["Build_ConstructorMk1_C"] = 1, ["Build_SmelterMk1_C"] = 2 }, belt.Machines);
        Assert.NotNull(belt.Position);
    }

    [Fact]
    public async Task Defining_a_factory_returns_it_and_its_location()
    {
        HttpResponseMessage response = await _client.DefineFactoryAsync("Development", "Iron plates", WorldClient.BeltIntoContainer, "Desc_OreIron_C");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<Factory>(TestContext.Current.CancellationToken);
        var readBack = await _client.GetAsync<Factory>(response.Headers.Location!.ToString());
        Assert.Equal(created, readBack with { InputItems = created!.InputItems });
        Assert.Equal(["Desc_OreIron_C"], readBack.InputItems);
    }

    [Fact]
    public async Task A_defined_factory_is_traced_in_a_snapshot_of_its_world()
    {
        Factory factory = await DefineIronPlatesAsync();

        var traces = await _client.GetAsync<FactoryTrace[]>($"/api/v1/world/snapshots/{_snapshot.Id}/factories");

        FactoryTrace trace = Assert.Single(traces);
        Assert.Equal(factory.Id, trace.FactoryId);
        Assert.True(trace.Traceable);
        Assert.Equal(
            [WorldClient.Id("Build_ConstructorMk1_C_2147291547"), WorldClient.Id("Build_SmelterMk1_C_2147301226"), WorldClient.Id("Build_SmelterMk1_C_2147301500")],
            trace.Machines.Select(machine => machine.Id));
    }

    [Fact]
    public async Task A_factory_is_traced_in_snapshots_imported_before_and_after_it_was_defined()
    {
        await DefineIronPlatesAsync();
        Snapshot later = await _client.ImportSaveAsync(WorldClient.TestWorld);

        foreach (Guid snapshot in new[] { _snapshot.Id, later.Id })
        {
            var traces = await _client.GetAsync<FactoryTrace[]>($"/api/v1/world/snapshots/{snapshot}/factories");
            Assert.Equal(3, Assert.Single(traces).Machines.Length);
        }
    }

    [Fact]
    public async Task Factories_are_listed_per_world()
    {
        Factory factory = await DefineIronPlatesAsync();

        var development = await _client.GetAsync<Factory[]>("/api/v1/world/factories?world=Development");
        var other = await _client.GetAsync<Factory[]>("/api/v1/world/factories?world=Somewhere%20else");

        Assert.Equal([factory.Id], development.Select(f => f.Id));
        Assert.Empty(other);
    }

    [Fact]
    public async Task Factory_names_are_unique_within_a_world()
    {
        await DefineIronPlatesAsync();

        HttpResponseMessage response = await _client.DefineFactoryAsync("Development", "Iron plates", WorldClient.BeltIntoContainer, "Desc_OreIron_C");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("World.FactoryNameTaken", (await ProblemAsync(response)).Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task A_factory_in_a_world_without_snapshots_is_unprocessable()
    {
        HttpResponseMessage response = await _client.DefineFactoryAsync("Somewhere else", "Iron plates", WorldClient.BeltIntoContainer);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("World.UnknownWorld", (await ProblemAsync(response)).Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task A_factory_whose_output_belt_is_not_a_belt_of_the_world_is_unprocessable()
    {
        HttpResponseMessage response = await _client.DefineFactoryAsync(
            "Development", "Iron plates", WorldClient.Id("Build_ConstructorMk1_C_2147291547"), "Desc_OreIron_C");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("World.NotAnOutputBelt", (await ProblemAsync(response)).Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task An_input_item_the_catalog_does_not_know_is_unprocessable()
    {
        HttpResponseMessage response = await _client.DefineFactoryAsync(
            "Development", "Iron plates", WorldClient.BeltIntoContainer, "Desc_OreIron_C", "Desc_IronOreTypo_C");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        ProblemDetails problem = await ProblemAsync(response);
        Assert.Equal("World.UnknownInputItems", problem.Extensions["code"]?.ToString());
        Assert.Equal("""["Desc_IronOreTypo_C"]""", problem.Extensions["items"]?.ToString());
    }

    [Fact]
    public async Task A_factory_needs_a_world_a_name_and_an_output_belt()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/world/factories", new { inputItems = OnlyIronOre }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal(["name", "outputBelt", "world"], problem!.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task The_factories_of_an_unknown_snapshot_are_not_found()
    {
        HttpResponseMessage response = await _client.GetAsync($"/api/v1/world/snapshots/{Guid.NewGuid()}/factories", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
