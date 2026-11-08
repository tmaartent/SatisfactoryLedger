namespace SatisfactoryLedger.World.SaveFile;

/// <param name="GameBuild">The game's build number, e.g. 517554. Saves and Docs files of the same build belong together.</param>
public sealed record SaveInfo(string SessionName, int GameBuild, DateTime SavedAtUtc);