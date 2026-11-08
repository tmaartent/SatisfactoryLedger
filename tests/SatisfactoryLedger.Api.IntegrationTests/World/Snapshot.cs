namespace SatisfactoryLedger.Api.IntegrationTests.World;

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