using System.Text.Json.Serialization;

namespace SatisfactoryLedger.World.Analysis;

/// <summary>Where some of what a factory takes in comes from, per minute (decided 2026-11-11).</summary>
/// <param name="FactoryId">For <see cref="InputSourceKind.Factory"/>: the supplying factory.</param>
/// <param name="Factory">For <see cref="InputSourceKind.Factory"/>: its name.</param>
/// <param name="Building">For <see cref="InputSourceKind.Building"/>: the class of the building, such as <c>Build_MinerMk3_C</c>.</param>
/// <param name="Id">For <see cref="InputSourceKind.Building"/>: the building itself, each on its own (decided 2026-11-11), to find it on the map.</param>
/// <param name="Machines">For <see cref="InputSourceKind.Factory"/> and <see cref="InputSourceKind.NoFactory"/>: the
/// machines and generators what arrives starts at. For the arrangement, which puts the spot such a source is reached at on
/// one of them (decided 2026-11-25); not in the API.</param>
internal sealed record InputSource(
    InputSourceKind Kind,
    Guid? FactoryId,
    string? Factory,
    string? Building,
    string? Id,
    double PerMinute,
    [property: JsonIgnore] IReadOnlyList<string>? Machines = null);
