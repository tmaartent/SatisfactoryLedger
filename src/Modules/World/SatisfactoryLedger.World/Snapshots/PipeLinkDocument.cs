namespace SatisfactoryLedger.World.Snapshots;

/// <summary>Two pipes (or a pipe and a junction, pump or buffer) that touch, without a direction.</summary>
internal sealed record PipeLinkDocument(string A, string B);
