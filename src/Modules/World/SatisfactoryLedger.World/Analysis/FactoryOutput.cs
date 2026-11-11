namespace SatisfactoryLedger.World.Analysis;

/// <summary>
/// What a factory makes of one item: on its output belt now, and if its machines all ran at 100% (BR-A14); where it
/// goes, what its receivers would take, what's sunk and what can't leave (BR-A18).
/// </summary>
/// <param name="Now">What flows onto the output belt per minute.</param>
/// <param name="AtFullSpeed">What the machines that feed the output belt make per minute at 100%, at their clock speeds.</param>
/// <param name="ReceiversNeed">
/// What the machines and generators its outputs reach would take per minute at 100%, from whatever source.
/// </param>
/// <param name="OthersBring">
/// What those receivers take of it now from other sources: what they take now, less what this factory's output brings them.
/// </param>
/// <param name="Short">What those receivers would take more at 100% than they take now, from all sources together.</param>
/// <param name="Sunk">What reaches an AWESOME Sink per minute.</param>
/// <param name="CannotLeave">What its output blocked machines would make more per minute at 100%, but can't get out.</param>
/// <param name="Destinations">Where what leaves goes, most first.</param>
internal sealed record FactoryOutput(
    string Item,
    double Now,
    double AtFullSpeed,
    double ReceiversNeed,
    double OthersBring,
    double Short,
    double Sunk,
    double CannotLeave,
    IReadOnlyList<OutputDestination> Destinations);