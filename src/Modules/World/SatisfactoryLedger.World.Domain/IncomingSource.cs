namespace SatisfactoryLedger.World.Domain;

/// <summary>Where some of what a factory takes in of an item comes from, per minute (decided 2026-11-11, BR-A19).</summary>
/// <param name="Owner">For <see cref="InputSourceKind.Factory"/>: the supplying factory.</param>
/// <param name="Building">For <see cref="InputSourceKind.Building"/>: the class of the building, such as <c>Build_MinerMk3_C</c>.</param>
/// <param name="Id">For <see cref="InputSourceKind.Building"/>: the building itself, each on its own (decided 2026-11-11).</param>
/// <param name="Machines">
/// For <see cref="InputSourceKind.Factory"/> and <see cref="InputSourceKind.NoFactory"/>: the machines and generators what
/// arrives starts at, ordered by id. None for the other kinds.
/// </param>
public sealed record IncomingSource(
    InputSourceKind Kind,
    Guid? Owner,
    string? Building,
    BuildableId? Id,
    double PerMinute,
    IReadOnlyList<BuildableId> Machines);
