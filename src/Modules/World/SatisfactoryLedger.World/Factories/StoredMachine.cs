using SatisfactoryLedger.World.Snapshots;

namespace SatisfactoryLedger.World.Factories;

/// <summary>
/// One of a factory's machines as stored: its id, and its class and position (centimetres) to find it again when it's
/// rebuilt with a new id (BR-W11). The position is null only when the save didn't give one.
/// </summary>
internal sealed record StoredMachine(string Id, string ClassName, double? X, double? Y, double? Z)
{
    public static StoredMachine From(BuildableDocument machine) =>
        new(machine.Id, machine.ClassName, machine.Position?.X, machine.Position?.Y, machine.Position?.Z);
}
