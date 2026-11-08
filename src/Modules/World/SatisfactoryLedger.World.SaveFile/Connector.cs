using SatisfactoryLedger.World.Domain;

namespace SatisfactoryLedger.World.SaveFile;

/// <summary>
/// One end of a link, as the save stores it: the port <paramref name="Path"/> of <paramref name="Owner"/>, linked to
/// the port <paramref name="LinkedTo"/> of another buildable, or to nothing.
/// </summary>
/// <param name="Index">Which output of a splitter or input of a merger this is, counted from 0; null elsewhere.</param>
internal sealed record Connector(BuildableId Owner, string Path, Port Port, string? LinkedTo, int? Index = null);