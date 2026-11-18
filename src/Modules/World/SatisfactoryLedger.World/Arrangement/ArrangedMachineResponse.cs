using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Arrangement;

/// <summary>One machine of an arranged factory, where it stands now: it moves with its factory to the proposed plot.</summary>
/// <param name="Position">Where it stands, in the game's centimetres.</param>
/// <param name="Yaw">Which way it faces, in degrees.</param>
internal sealed record ArrangedMachineResponse(Guid FactoryId, string Id, string ClassName, PositionDocument Position, double Yaw);
