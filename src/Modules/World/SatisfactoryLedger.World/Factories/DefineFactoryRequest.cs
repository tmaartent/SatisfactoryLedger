namespace SatisfactoryLedger.World.Factories;

/// <param name="Machines">The factory's machines (BR-W2).</param>
/// <param name="OutputBelt">Instead of machines, until the 3D selection replaces the old dialog: an output belt to trace them from.</param>
/// <param name="InputItems">With an output belt: the items where the trace stops.</param>
internal sealed record DefineFactoryRequest(string? World, string? Name, string[]? Machines, string? OutputBelt, string[]? InputItems);
