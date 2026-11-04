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
    UnsupportedClass[] Unsupported);

public sealed record SnapshotSummary(Guid Id, string SessionName, int GameBuild, DateTimeOffset SavedAt, DateTimeOffset ImportedAt, SnapshotCounts Counts);

internal static class WorldClient
{
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

    public static async Task<Snapshot> ImportSaveAsync(this HttpClient client, string fixture)
    {
        HttpResponseMessage response = await client.PostSaveAsync(fixture);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Snapshot>(TestContext.Current.CancellationToken))!;
    }
}
