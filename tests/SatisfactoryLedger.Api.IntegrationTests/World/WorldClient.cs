using System.Net.Http.Json;

namespace SatisfactoryLedger.Api.IntegrationTests.World;

public sealed record SnapshotCounts(int Machines, int Providers, int Consumers, int Logistics, int Connections);

public sealed record UnsupportedClass(string ClassName, int Count);

public sealed record Snapshot(
    Guid Id,
    string SessionName,
    int GameBuild,
    DateTimeOffset SavedAt,
    DateTimeOffset ImportedAt,
    SnapshotCounts Counts,
    string[] UnknownRecipes,
    UnsupportedClass[] Unsupported,
    string Analysis);

public sealed record SnapshotSummary(Guid Id, string SessionName, int GameBuild, DateTimeOffset SavedAt, DateTimeOffset ImportedAt, SnapshotCounts Counts);

public sealed record Reference(string Id, string ClassName);

public sealed record Position(double X, double Y, double Z);

public sealed record OutputBelt(string Belt, string ClassName, string[] Items, Reference[] Into, Position? Position, Dictionary<string, int> Machines);

public sealed record Factory(Guid Id, string World, string Name, string OutputBelt, string[] InputItems);

public sealed record Machine(string Id, string ClassName, string? Recipe);

public sealed record FactoryTrace(Guid FactoryId, string Name, bool Traceable, Machine[] Machines, int Logistics);

internal static class WorldClient
{
    public static string Id(string name) => $"Persistent_Level:PersistentLevel.{name}";

    public static readonly string BeltIntoContainer = Id("Build_ConveyorBeltMk1_C_2147276358");

    public static Task<HttpResponseMessage> DefineFactoryAsync(
        this HttpClient client, string world, string name, string outputBelt, params string[] inputItems) =>
        client.PostAsJsonAsync("/api/v1/world/factories", new { world, name, outputBelt, inputItems }, TestContext.Current.CancellationToken);

    /// <summary>The test world, linked from the save-file adapter's tests (see docs/guide/log/07).</summary>
    public const string TestWorld = "Fixtures/Development.sav";

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
