namespace SatisfactoryLedger.Api.IntegrationTests.World;

public sealed record SnapshotSummary(Guid Id, string SessionName, int GameBuild, DateTimeOffset SavedAt, DateTimeOffset ImportedAt, SnapshotCounts Counts);