using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using SatisfactoryLedger.Api.IntegrationTests.Catalog;
using SatisfactoryLedger.World.Factories;
using SatisfactoryLedger.World.Persistence;

namespace SatisfactoryLedger.Api.IntegrationTests.World;

/// <summary>
/// Factory definitions over the API: defining by machines or the old way, converting old ones, tracing, and suggested
/// output belts (BR-W2, BR-W4, BR-W11, BR-W12).
/// </summary>
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
        HttpResponseMessage response = await _client.DefineFactoryAsync("Development", "Iron plates", WorldClient.IronPlateMachines);
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
    public async Task Defining_a_factory_by_its_machines_returns_it_and_its_location()
    {
        HttpResponseMessage response = await _client.DefineFactoryAsync("Development", "Iron plates", WorldClient.IronPlateMachines);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<Factory>(TestContext.Current.CancellationToken))!;
        var readBack = await _client.GetAsync<Factory>(response.Headers.Location!.ToString());
        Assert.Equal((created.Id, created.Name), (readBack.Id, readBack.Name));
        Assert.Equal(WorldClient.IronPlateMachines, readBack.Machines.Select(machine => machine.Id));
        Assert.Equal(["Build_ConstructorMk1_C", "Build_SmelterMk1_C", "Build_SmelterMk1_C"], readBack.Machines.Select(machine => machine.ClassName));
    }

    [Fact]
    public async Task A_factory_defined_by_its_output_belt_is_stored_as_the_machines_traced_from_it()
    {
        HttpResponseMessage response = await _client.DefineFactoryByOutputBeltAsync("Development", "Iron plates", WorldClient.BeltIntoContainer, "Desc_OreIron_C");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<Factory>(TestContext.Current.CancellationToken))!;
        Assert.Equal(WorldClient.IronPlateMachines, created.Machines.Select(machine => machine.Id));
        Assert.Null(created.OutputBelt);
    }

    [Fact]
    public async Task A_factory_defined_by_its_output_belt_before_is_converted_to_its_machines()
    {
        Guid traceable = await StoreOldFactoryAsync("Iron plates", WorldClient.BeltIntoContainer);
        Guid gone = await StoreOldFactoryAsync("Gone", WorldClient.Id("Build_ConveyorBeltMk1_C_1"));

        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            IReadOnlyList<string> notConverted = await FactoryConversion.ConvertAsync(
                scope.ServiceProvider.GetRequiredService<WorldDbContext>(), TestContext.Current.CancellationToken);
            Assert.Equal(["Development: Gone"], notConverted);
        }

        Factory converted = await _client.GetAsync<Factory>($"/api/v1/world/factories/{traceable}");
        Factory kept = await _client.GetAsync<Factory>($"/api/v1/world/factories/{gone}");
        Assert.Equal(WorldClient.IronPlateMachines, converted.Machines.Select(machine => machine.Id));
        Assert.Equal((0, WorldClient.Id("Build_ConveyorBeltMk1_C_1")), (kept.Machines.Length, kept.OutputBelt));
    }

    /// <summary>A factory as the old dialog stored it, written straight to the database.</summary>
    private async Task<Guid> StoreOldFactoryAsync(string name, string outputBelt)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        var factory = new StoredFactory
        {
            Id = Guid.CreateVersion7(),
            World = "Development",
            Name = name,
            OutputBelt = outputBelt,
            InputItems = ["Desc_OreIron_C"],
        };
        db.Factories.Add(factory);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return factory.Id;
    }

    [Fact]
    public async Task A_defined_factory_is_traced_in_a_snapshot_of_its_world()
    {
        Factory factory = await DefineIronPlatesAsync();

        var traces = await _client.GetAsync<FactoryTrace[]>($"/api/v1/world/snapshots/{_snapshot.Id}/factories");

        FactoryTrace trace = Assert.Single(traces);
        Assert.Equal(factory.Id, trace.FactoryId);
        Assert.True(trace.Traceable);
        Assert.Equal(WorldClient.IronPlateMachines, trace.Machines.Select(machine => machine.Id));
        Assert.Empty(trace.Missing);
    }

    [Fact]
    public async Task A_traced_factory_lists_its_outputs_and_inputs()
    {
        await DefineIronPlatesAsync();

        FactoryTrace trace = Assert.Single(await _client.GetAsync<FactoryTrace[]>($"/api/v1/world/snapshots/{_snapshot.Id}/factories"));

        Assert.Equal(["Build_ResourceSink_C", null], trace.Outputs.Select(output => output.Outside?.ClassName).Order(StringComparer.Ordinal).Reverse());
        Assert.All(trace.Outputs, output => Assert.Equal(["Desc_IronPlate_C"], output.Items));
        Assert.Equal(["Desc_OreIron_C"], Assert.Single(trace.Inputs).Items);
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

        HttpResponseMessage response = await _client.DefineFactoryAsync("Development", "Iron plates", WorldClient.IronPlateMachines);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("World.FactoryNameTaken", (await ProblemAsync(response)).Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task A_factory_in_a_world_without_snapshots_is_unprocessable()
    {
        HttpResponseMessage response = await _client.DefineFactoryAsync("Somewhere else", "Iron plates", WorldClient.IronPlateMachines);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("World.UnknownWorld", (await ProblemAsync(response)).Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task A_factory_of_ids_that_are_not_machines_of_the_world_is_unprocessable()
    {
        HttpResponseMessage response = await _client.DefineFactoryAsync(
            "Development", "Iron plates", WorldClient.IronPlateMachines[0], WorldClient.BeltIntoContainer);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        ProblemDetails problem = await ProblemAsync(response);
        Assert.Equal("World.NotMachines", problem.Extensions["code"]?.ToString());
        Assert.Equal($"[\"{WorldClient.BeltIntoContainer}\"]", problem.Extensions["ids"]?.ToString());
    }

    [Fact]
    public async Task A_factory_whose_output_belt_is_not_a_belt_of_the_world_is_unprocessable()
    {
        HttpResponseMessage response = await _client.DefineFactoryByOutputBeltAsync(
            "Development", "Iron plates", WorldClient.Id("Build_ConstructorMk1_C_2147291547"), "Desc_OreIron_C");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("World.NotAnOutputBelt", (await ProblemAsync(response)).Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task An_input_item_the_catalog_does_not_know_is_unprocessable()
    {
        HttpResponseMessage response = await _client.DefineFactoryByOutputBeltAsync(
            "Development", "Iron plates", WorldClient.BeltIntoContainer, "Desc_OreIron_C", "Desc_IronOreTypo_C");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        ProblemDetails problem = await ProblemAsync(response);
        Assert.Equal("World.UnknownInputItems", problem.Extensions["code"]?.ToString());
        Assert.Equal("""["Desc_IronOreTypo_C"]""", problem.Extensions["items"]?.ToString());
    }

    [Fact]
    public async Task A_factory_needs_a_world_a_name_and_machines()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/world/factories", new { inputItems = OnlyIronOre }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal(["machines", "name", "world"], problem!.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_factory_is_given_by_its_machines_or_by_an_output_belt_not_both()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/world/factories",
            new { world = "Development", name = "Iron plates", machines = WorldClient.IronPlateMachines, outputBelt = WorldClient.BeltIntoContainer },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_factories_of_an_unknown_snapshot_are_not_found()
    {
        HttpResponseMessage response = await _client.GetAsync($"/api/v1/world/snapshots/{Guid.NewGuid()}/factories", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
