using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using SatisfactoryLedger.Api.IntegrationTests.Catalog;
using SatisfactoryLedger.World.Factories;
using SatisfactoryLedger.World.Persistence;

namespace SatisfactoryLedger.Api.IntegrationTests.World;

/// <summary>
/// Factory definitions over the API: defining by machines, and tracing (BR-W2, BR-W4, BR-W11).
/// </summary>
public sealed class WorldFactoryApiTests(LedgerApiFactory api) : IAsyncLifetime
{
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
    public async Task A_generator_can_be_one_of_a_factorys_machines()
    {
        await _client.ImportSaveAsync(WorldClient.WorldWithGenerators);

        HttpResponseMessage response = await _client.DefineFactoryAsync("Development", "Power", WorldClient.NuclearPlant);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_machine_of_one_factory_cannot_be_in_another()
    {
        await DefineIronPlatesAsync();

        HttpResponseMessage response = await _client.DefineFactoryAsync("Development", "Smelting", WorldClient.IronPlateMachines[1]);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        ProblemDetails problem = await ProblemAsync(response);
        Assert.Equal("World.MachinesTaken", problem.Extensions["code"]?.ToString());
        Assert.Contains("Iron plates", problem.Extensions["taken"]?.ToString(), StringComparison.Ordinal);
        Assert.Contains(WorldClient.IronPlateMachines[1], problem.Extensions["taken"]?.ToString(), StringComparison.Ordinal);
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
    public async Task A_factory_needs_a_world_a_name_and_machines()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/world/factories", new { }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal(["machines", "name", "world"], problem!.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Previewing_chosen_machines_says_what_the_factory_would_own_and_hand_over_without_storing_it()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/v1/world/snapshots/{_snapshot.Id}/factory-preview", new { machines = WorldClient.IronPlateMachines }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = (await response.Content.ReadFromJsonAsync<FactoryPreview>(TestContext.Current.CancellationToken))!;
        Assert.Contains(WorldClient.BeltIntoContainer, preview.Logistics);
        Assert.Contains(preview.Outputs, output => output.Items.Contains("Desc_IronPlate_C"));
        Assert.Empty(await _client.GetAsync<Factory[]>("/api/v1/world/factories?world=Development"));
    }

    [Fact]
    public async Task A_preview_in_an_unknown_snapshot_is_not_found()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/v1/world/snapshots/{Guid.NewGuid()}/factory-preview", new { machines = WorldClient.IronPlateMachines }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_factories_of_an_unknown_snapshot_are_not_found()
    {
        HttpResponseMessage response = await _client.GetAsync($"/api/v1/world/snapshots/{Guid.NewGuid()}/factories", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
