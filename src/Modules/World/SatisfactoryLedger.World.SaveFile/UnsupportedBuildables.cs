namespace SatisfactoryLedger.World.SaveFile;

/// <summary>Buildables in the item network the adapter can't model yet, such as stations. Their connections are left out.</summary>
public sealed record UnsupportedBuildables(string ClassName, int Count);