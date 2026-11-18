using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Arrangement;

/// <summary>The machines of a run's arranged factories as they stand now, to draw as the map does (decided 2026-11-18).</summary>
/// <param name="Footprints">The ground each of their classes covers, by class name: turned with each machine.</param>
internal sealed record ArrangementMachinesResponse(IReadOnlyList<ArrangedMachineResponse> Machines, IReadOnlyList<FootprintDocument> Footprints);
