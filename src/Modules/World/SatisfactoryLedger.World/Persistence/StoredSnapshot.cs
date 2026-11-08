using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Persistence;

/// <summary>
/// One imported save, as stored (ADR-0016): columns for what lists need, and the whole content as one jsonb document.
/// A persistence record, not a domain entity: the domain's <c>WorldGraph</c> is built from <see cref="Content"/> when needed.
/// </summary>
internal sealed class StoredSnapshot
{
    public required Guid Id { get; init; }

    public required string SessionName { get; init; }

    public required int GameBuild { get; init; }

    public required DateTimeOffset SavedAt { get; init; }

    public required DateTimeOffset ImportedAt { get; init; }

    public required SnapshotCounts Counts { get; init; }

    public required List<string> UnknownRecipes { get; init; }

    public required List<UnsupportedDocument> Unsupported { get; init; }

    /// <summary>The <see cref="SnapshotDocument"/> as JSON.</summary>
    public required string Content { get; init; }
}