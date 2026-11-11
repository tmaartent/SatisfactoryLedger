namespace SatisfactoryLedger.World.Analysis;

/// <summary>The power a factory's generators make, in MW (decided 2026-11-11).</summary>
/// <param name="NowMegawatts">At the speed the flow lets them run.</param>
/// <param name="AtFullSpeedMegawatts">If they all ran at 100%, at their clock speeds.</param>
internal sealed record FactoryPower(double NowMegawatts, double AtFullSpeedMegawatts);
