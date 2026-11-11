using System.Net.Http.Json;

namespace SatisfactoryLedger.Api.IntegrationTests.World;

internal static class WorldClient
{
    public static string Id(string name) => $"Persistent_Level:PersistentLevel.{name}";

    public static readonly string BeltIntoContainer = Id("Build_ConveyorBeltMk1_C_2147276358");

    /// <summary>The test world's iron plate factory: two smelters and a constructor.</summary>
    public static readonly string[] IronPlateMachines =
        [Id("Build_ConstructorMk1_C_2147291547"), Id("Build_SmelterMk1_C_2147301226"), Id("Build_SmelterMk1_C_2147301500")];

    public static Task<HttpResponseMessage> DefineFactoryAsync(this HttpClient client, string world, string name, params string[] machines) =>
        client.PostAsJsonAsync("/api/v1/world/factories", new { world, name, machines }, TestContext.Current.CancellationToken);

    /// <summary>The old way, until the 3D selection replaces the dialog: by output belt and input items.</summary>
    public static Task<HttpResponseMessage> DefineFactoryByOutputBeltAsync(
        this HttpClient client, string world, string name, string outputBelt, params string[] inputItems) =>
        client.PostAsJsonAsync("/api/v1/world/factories", new { world, name, outputBelt, inputItems }, TestContext.Current.CancellationToken);

    /// <summary>The test world, linked from the save-file adapter's tests (see docs/guide/log/07).</summary>
    public const string TestWorld = "Fixtures/Development.sav";

    /// <summary>The test world saved again with a nuclear plant, a coal and a fuel generator (see docs/guide/log/23).</summary>
    public const string WorldWithGenerators = "Fixtures/Generators.sav";

    public static readonly string NuclearPlant = Id("Build_GeneratorNuclear_C_2147427188");

    public static async Task<HttpResponseMessage> PostSaveAsync(this HttpClient client, byte[] save, string fileName = "Development.sav")
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(save), "file", fileName);
        return await client.PostAsync("/api/v1/world/snapshots", form, TestContext.Current.CancellationToken);
    }

    public static Task<HttpResponseMessage> PostSaveAsync(this HttpClient client, string fixture) =>
        client.PostSaveAsync(File.ReadAllBytes(fixture), Path.GetFileName(fixture));

    /// <summary>
    /// Waits until the background worker has analysed the snapshot (ADR-0017): the test world takes well under a
    /// second, so a minute is plenty even on a slow machine.
    /// </summary>
    public static async Task WaitForAnalysisAsync(this HttpClient client, Guid snapshotId)
    {
        DateTime giveUp = DateTime.UtcNow.AddMinutes(1);
        while (DateTime.UtcNow < giveUp)
        {
            Snapshot? snapshot = await client.GetFromJsonAsync<Snapshot>($"/api/v1/world/snapshots/{snapshotId}", TestContext.Current.CancellationToken);
            if (snapshot!.Analysis != "pending")
            {
                return;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"The analysis of snapshot {snapshotId} wasn't ready after a minute");
    }

    public static async Task<Snapshot> ImportSaveAsync(this HttpClient client, string fixture)
    {
        HttpResponseMessage response = await client.PostSaveAsync(fixture);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Snapshot>(TestContext.Current.CancellationToken))!;
    }
}