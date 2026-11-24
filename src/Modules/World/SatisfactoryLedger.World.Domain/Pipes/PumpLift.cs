namespace SatisfactoryLedger.World.Domain.Pipes;

/// <summary>What a pump does to fluid (BR-A22): past it, towards <paramref name="Outlet"/>, fluid rises up to <paramref name="HeadLiftMetres"/> above it.</summary>
/// <param name="Outlet">The pipe piece on its outlet, which it pushes into.</param>
public sealed record PumpLift(double HeadLiftMetres, BuildableId Outlet);
