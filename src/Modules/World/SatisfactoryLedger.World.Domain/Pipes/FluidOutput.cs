namespace SatisfactoryLedger.World.Domain.Pipes;

/// <summary>
/// A machine's or extractor's fluid output, where it feeds <paramref name="Pipe"/> (BR-A22): it lifts fluid
/// <see cref="HeadLift.OfFluidOutput"/> above <paramref name="HeightMetres"/>, the height of the pipe's end there.
/// </summary>
public sealed record FluidOutput(BuildableId Pipe, double HeightMetres);
